using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Domain.Common;

namespace Mundialito.Infrastructure.Events;

/// <summary>
/// Logs every domain event with structured fields (type, id, timestamp) and invokes the
/// registered <see cref="IDomainEventHandler{TEvent}"/> implementations for its runtime type.
/// </summary>
internal sealed partial class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> Invokers = new();

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType();

            LogDomainEvent(logger, eventType.Name, domainEvent.EventId, domainEvent.OccurredAtUtc);

            // The event also shows up on the current trace span, so Jaeger links it to the request.
            Activity.Current?.AddEvent(new ActivityEvent(
                "domain.event",
                tags: new ActivityTagsCollection
                {
                    ["domain.event.type"] = eventType.Name,
                    ["domain.event.id"] = domainEvent.EventId.ToString(),
                }));

            var invoker = Invokers.GetOrAdd(
                eventType,
                static type => (HandlerInvoker)Activator.CreateInstance(typeof(HandlerInvoker<>).MakeGenericType(type))!);

            await invoker.InvokeAsync(domainEvent, serviceProvider, cancellationToken);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Domain event {DomainEventType} {DomainEventId} occurred at {OccurredAtUtc}")]
    private static partial void LogDomainEvent(ILogger logger, string domainEventType, Guid domainEventId, DateTime occurredAtUtc);

    private abstract class HandlerInvoker
    {
        public abstract Task InvokeAsync(IDomainEvent domainEvent, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    }

    private sealed class HandlerInvoker<TEvent> : HandlerInvoker
        where TEvent : IDomainEvent
    {
        public override async Task InvokeAsync(IDomainEvent domainEvent, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var typedEvent = (TEvent)domainEvent;

            foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
            {
                await handler.Handle(typedEvent, cancellationToken);
            }
        }
    }
}
