namespace Mundialito.Application.DomainEvents;

/// <summary>Read model of a persisted domain event. The payload is returned as raw JSON text.</summary>
public sealed record DomainEventLogResponse(
    Guid Id,
    string EventType,
    Guid AggregateId,
    string Payload,
    string? CorrelationId,
    string? TraceId,
    DateTime OccurredAtUtc,
    DateTime RecordedAtUtc);
