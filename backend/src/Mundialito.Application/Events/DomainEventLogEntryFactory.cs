using System.Text.Json;
using System.Text.Json.Serialization;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Events;

/// <summary>Builds the persisted trace of an event: type name, JSON payload and request identifiers.</summary>
internal static class DomainEventLogEntryFactory
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static DomainEventLogEntry Create<TEvent>(
        TEvent domainEvent,
        Guid aggregateId,
        IRequestContext requestContext,
        IClock clock)
        where TEvent : IDomainEvent
    {
        // Serialized as the concrete type so every property of the event ends up in the payload.
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), PayloadOptions);

        return new DomainEventLogEntry(
            domainEvent.EventId,
            typeof(TEvent).Name,
            aggregateId,
            payload,
            requestContext.CorrelationId,
            requestContext.TraceId,
            domainEvent.OccurredAtUtc,
            clock.UtcNow);
    }
}
