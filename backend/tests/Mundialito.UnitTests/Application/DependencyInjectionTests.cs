using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mundialito.Application;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Common;

namespace Mundialito.UnitTests.Application;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ShouldResolveSenderWithBothBehaviors()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISender>().ShouldNotBeNull();

        var behaviors = scope.ServiceProvider
            .GetServices<IPipelineBehavior<ProbeCommand, Result>>()
            .Select(b => b.GetType().Name)
            .ToArray();

        behaviors.ShouldBe(["LoggingBehavior`2", "ValidationBehavior`2"]);
    }

    public sealed record ProbeCommand : ICommand;
}
