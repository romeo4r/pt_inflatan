using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Domain.Teams.Events;
using Mundialito.Infrastructure.Events;

namespace Mundialito.UnitTests.Infrastructure.Events;

public class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ShouldInvokeEveryHandlerForTheEventType()
    {
        var calls = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(calls);
        services.AddScoped<IDomainEventHandler<TeamCreatedDomainEvent>, FirstHandler>();
        services.AddScoped<IDomainEventHandler<TeamCreatedDomainEvent>, SecondHandler>();
        using var provider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider, NullLogger<DomainEventDispatcher>.Instance);
        var domainEvent = new TeamCreatedDomainEvent(Guid.CreateVersion7(), "Finanzas", "FIN");

        await dispatcher.DispatchAsync([domainEvent], CancellationToken.None);

        calls.ShouldBe(["first:FIN", "second:FIN"]);
    }

    [Fact]
    public async Task DispatchAsync_WithoutHandlers_ShouldNotFail()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider, NullLogger<DomainEventDispatcher>.Instance);

        await Should.NotThrowAsync(() => dispatcher.DispatchAsync(
            [new TeamCreatedDomainEvent(Guid.CreateVersion7(), "Finanzas", "FIN")],
            CancellationToken.None));
    }

    private sealed class FirstHandler(List<string> calls) : IDomainEventHandler<TeamCreatedDomainEvent>
    {
        public Task Handle(TeamCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            calls.Add($"first:{domainEvent.Code}");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondHandler(List<string> calls) : IDomainEventHandler<TeamCreatedDomainEvent>
    {
        public Task Handle(TeamCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            calls.Add($"second:{domainEvent.Code}");
            return Task.CompletedTask;
        }
    }
}
