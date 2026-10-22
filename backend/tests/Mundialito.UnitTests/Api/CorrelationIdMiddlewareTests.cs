using Microsoft.AspNetCore.Http;
using Mundialito.Api.Middleware;

namespace Mundialito.UnitTests.Api;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Invoke_WithValidIncomingHeader_ShouldReuseIt()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "order-42_a.b";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.GetCorrelationId().ShouldBe("order-42_a.b");
        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString().ShouldBe("order-42_a.b");
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad value with spaces")]
    [InlineData("inject\nnewline")]
    [InlineData("<script>")]
    public async Task Invoke_WithUnsafeHeader_ShouldGenerateNewId(string incoming)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = incoming;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = context.GetCorrelationId();
        correlationId.ShouldNotBe(incoming);
        Guid.TryParse(correlationId, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Invoke_WithHeaderTooLong_ShouldGenerateNewId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = new string('a', 65);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Guid.TryParse(context.GetCorrelationId(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Invoke_ShouldExposeTraceIdAndCallNext()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.GetTraceId().ShouldNotBeNullOrWhiteSpace();
        context.Response.Headers[CorrelationIdMiddleware.TraceIdHeader].ToString().ShouldBe(context.GetTraceId());
    }
}
