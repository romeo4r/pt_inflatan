namespace Mundialito.Domain.Common;

/// <summary>
/// Something meaningful that happened inside the domain. Events are collected by the aggregate
/// and dispatched by the Unit of Work after a successful commit.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredAtUtc { get; }
}

/// <summary>
/// Base record for domain events. Records give value equality and a concise syntax.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}
