using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Abstractions.Idempotency;
using NSubstitute;

namespace Mundialito.UnitTests.Api;

public class IdempotencyMiddlewareTests
{
    private readonly IIdempotencyStore _store = Substitute.For<IIdempotencyStore>();

    [Fact]
    public async Task Invoke_OnEndpointWithoutMetadata_ShouldPassThrough()
    {
        var context = CreateContext(idempotent: false, key: null);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, _store);

        nextCalled.ShouldBeTrue();
        await _store.DidNotReceiveWithAnyArgs().TryReserveAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task Invoke_WithoutKey_ShouldReturn400AndSkipHandler()
    {
        var context = CreateContext(idempotent: true, key: null);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, _store);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        (await ReadBody(context)).ShouldContain("Idempotency.KeyRequired");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public async Task Invoke_WithInvalidKey_ShouldReturn400(string key)
    {
        var context = CreateContext(idempotent: true, key);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, _store);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        (await ReadBody(context)).ShouldContain("Idempotency.KeyInvalid");
    }

    [Fact]
    public async Task Invoke_FirstCall_ShouldRunHandlerStoreResponseAndStreamIt()
    {
        var context = CreateContext(idempotent: true, key: "abc-123", body: "{\"name\":\"Finanzas\"}");
        _store.TryReserveAsync("abc-123", Arg.Any<string>(), "POST", "/api/teams", Arg.Any<CancellationToken>())
            .Returns(new IdempotencyReservation(ReservationOutcome.Reserved));
        var middleware = CreateMiddleware(async ctx =>
        {
            ctx.Response.StatusCode = 201;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers.Location = "/api/teams/1";
            await ctx.Response.WriteAsync("{\"id\":1}");
        });

        await middleware.InvokeAsync(context, _store);

        context.Response.StatusCode.ShouldBe(201);
        (await ReadBody(context)).ShouldBe("{\"id\":1}");
        await _store.Received(1).CompleteAsync(
            "abc-123",
            Arg.Is<IdempotentResponse>(r => r.StatusCode == 201 && r.Body == "{\"id\":1}" && r.Location == "/api/teams/1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invoke_ShouldHashMethodPathAndBody()
    {
        var context = CreateContext(idempotent: true, key: "k", body: "{\"a\":1}");
        string? capturedHash = null;
        _store.TryReserveAsync("k", Arg.Do<string>(h => capturedHash = h), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyReservation(ReservationOutcome.Reserved));
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, _store);

        capturedHash.ShouldNotBeNull();
        capturedHash.Length.ShouldBe(64);
        // The handler must still be able to read the body after hashing.
        context.Request.Body.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Invoke_ReplayedCall_ShouldReturnStoredResponseWithoutHandler()
    {
        var context = CreateContext(idempotent: true, key: "abc-123");
        _store.TryReserveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyReservation(
                ReservationOutcome.Completed,
                new IdempotentResponse(201, "application/json", "{\"id\":1}", "/api/teams/1")));
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, _store);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(201);
        context.Response.Headers.Location.ToString().ShouldBe("/api/teams/1");
        context.Response.Headers[IdempotencyMiddleware.ReplayedHeaderName].ToString().ShouldBe("true");
        (await ReadBody(context)).ShouldBe("{\"id\":1}");
    }

    [Theory]
    [InlineData(ReservationOutcome.PayloadMismatch, "Idempotency.PayloadMismatch")]
    [InlineData(ReservationOutcome.InProgress, "Idempotency.RequestInProgress")]
    public async Task Invoke_OnConflict_ShouldReturn409(ReservationOutcome outcome, string expectedCode)
    {
        var context = CreateContext(idempotent: true, key: "abc-123");
        _store.TryReserveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyReservation(outcome));
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, _store);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        (await ReadBody(context)).ShouldContain(expectedCode);
    }

    [Fact]
    public async Task Invoke_WhenHandlerReturns500_ShouldReleaseKey()
    {
        var context = CreateContext(idempotent: true, key: "abc-123");
        _store.TryReserveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyReservation(ReservationOutcome.Reserved));
        var middleware = CreateMiddleware(ctx => { ctx.Response.StatusCode = 500; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, _store);

        await _store.Received(1).ReleaseAsync("abc-123", Arg.Any<CancellationToken>());
        await _store.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Invoke_WhenHandlerThrows_ShouldReleaseKeyAndRethrow()
    {
        var context = CreateContext(idempotent: true, key: "abc-123");
        _store.TryReserveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyReservation(ReservationOutcome.Reserved));
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, _store));

        await _store.Received(1).ReleaseAsync("abc-123", Arg.Any<CancellationToken>());
    }

    private static IdempotencyMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<IdempotencyMiddleware>.Instance);

    private static DefaultHttpContext CreateContext(bool idempotent, string? key, string body = "{}")
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider(),
        };
        context.Request.Method = "POST";
        context.Request.Path = "/api/teams";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();

        if (key is not null)
        {
            context.Request.Headers[IdempotencyMiddleware.HeaderName] = key;
        }

        var metadata = idempotent ? new EndpointMetadataCollection(new IdempotentEndpointMetadata()) : EndpointMetadataCollection.Empty;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));

        return context;
    }

    private static async Task<string> ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
