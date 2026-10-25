namespace Mundialito.Application.Abstractions.Persistence;

/// <summary>Persisted trace of a domain event. Written by event handlers, read by the audit endpoint.</summary>
public sealed record DomainEventLogEntry(
    Guid Id,
    string EventType,
    Guid AggregateId,
    string Payload,
    string? CorrelationId,
    string? TraceId,
    DateTime OccurredAtUtc,
    DateTime RecordedAtUtc);

/// <summary>
/// Write side of the event log. Entries are tracked on the same unit of work as the change that
/// raised the event, so they are committed or rolled back together.
/// </summary>
public interface IDomainEventLogRepository
{
    void Add(DomainEventLogEntry entry);
}
