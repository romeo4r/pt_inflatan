using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Common;
using Mundialito.UnitTests.Application.Messaging;
using NSubstitute;

namespace Mundialito.UnitTests.Application.Behaviors;

public class LoggingBehaviorTests
{
    [Fact]
    public async Task Handle_OnSuccess_ShouldLogInformation()
    {
        var logger = CreateLogger();
        var sender = BuildSender(logger, Result.Success());

        await sender.Send(new SampleCommand());

        AssertLogged(logger, LogLevel.Information, "Completed SampleCommand");
    }

    [Fact]
    public async Task Handle_OnFailure_ShouldLogWarningWithErrorCode()
    {
        var logger = CreateLogger();
        var sender = BuildSender(logger, Result.Failure(Error.NotFound("Sample.NotFound", "no existe")));

        await sender.Send(new SampleCommand());

        AssertLogged(logger, LogLevel.Warning, "Sample.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldNotAlterResponse()
    {
        var error = Error.Conflict("Sample.Conflict", "conflicto");
        var sender = BuildSender(CreateLogger(), Result.Failure(error));

        var result = await sender.Send(new SampleCommand());

        result.Error.ShouldBe(error);
    }

    private static ILogger CreateLogger()
    {
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        return logger;
    }

    private static void AssertLogged(ILogger logger, LogLevel level, string fragment)
    {
        logger.Received(1).Log(
            level,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(fragment)),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    private static ISender BuildSender(ILogger logger, Result handlerResult)
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddScoped(typeof(ISender), DispatcherAccessor.DispatcherType);
        services.AddScoped(typeof(IPipelineBehavior<,>), DispatcherAccessor.LoggingBehaviorType);
        services.AddScoped<IRequestHandler<SampleCommand, Result>>(_ => new SampleHandler(handlerResult));

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    public sealed record SampleCommand : ICommand;

    private sealed class SampleHandler(Result result) : ICommandHandler<SampleCommand>
    {
        public Task<Result> Handle(SampleCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
