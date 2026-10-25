using Mundialito.Application.Abstractions.Persistence;

namespace Mundialito.Infrastructure.Persistence.DomainEvents;

/// <summary>Row of the DomainEventLogs table. Append-only: rows are never updated or deleted by the application.</summary>
public sealed class DomainEventLogRecord
{
    public const int EventTypeMaxLength = 200;
    public const int IdentifierMaxLength = 64;

    public required Guid Id { get; init; }

    public required string EventType { get; init; }

    public required Guid AggregateId { get; init; }

    public required string Payload { get; init; }

    public string? CorrelationId { get; init; }

    public string? TraceId { get; init; }

    public required DateTime OccurredAtUtc { get; init; }

    public required DateTime RecordedAtUtc { get; init; }

    public static DomainEventLogRecord From(DomainEventLogEntry entry) => new()
    {
        Id = entry.Id,
        EventType = entry.EventType,
        AggregateId = entry.AggregateId,
        Payload = entry.Payload,
        CorrelationId = entry.CorrelationId,
        TraceId = entry.TraceId,
        OccurredAtUtc = entry.OccurredAtUtc,
        RecordedAtUtc = entry.RecordedAtUtc,
    };
}
