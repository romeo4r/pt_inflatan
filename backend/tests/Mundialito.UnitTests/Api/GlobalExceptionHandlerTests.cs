using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Mundialito.Api.Middleware;
using NSubstitute;

namespace Mundialito.UnitTests.Api;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandle_WithUnexpectedException_ShouldWrite500ProblemWithoutExceptionDetails()
    {
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        problemDetailsService.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(ValueTask.FromResult(true));
        var handler = new GlobalExceptionHandler(problemDetailsService, NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        var exception = new InvalidOperationException("secret connection string inside");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        await problemDetailsService.Received(1).TryWriteAsync(Arg.Is<ProblemDetailsContext>(c =>
            c.ProblemDetails.Status == 500
            && c.ProblemDetails.Extensions["code"]!.Equals("Server.Unexpected")
            && !c.ProblemDetails.Detail!.Contains("secret")));
    }

    [Fact]
    public async Task TryHandle_WhenClientCancelled_ShouldReturn499WithoutBody()
    {
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        var handler = new GlobalExceptionHandler(problemDetailsService, NullLogger<GlobalExceptionHandler>.Instance);
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(499);
        await problemDetailsService.DidNotReceive().TryWriteAsync(Arg.Any<ProblemDetailsContext>());
    }
}
