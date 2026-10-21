namespace Mundialito.Domain.Common;

/// <summary>
/// Base class for every persisted domain object. Identity is a time-ordered GUID (version 7),
/// generated in the domain so an entity has an id before it is ever stored.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(Guid id)
    {
        Id = id;
    }

    /// <summary>Required by EF Core for materialization.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; init; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    protected static Guid NewId() => Guid.CreateVersion7();
}

/// <summary>
/// Entity with creation/update timestamps. The values are stamped by Infrastructure at commit time,
/// which keeps the domain free of clock dependencies.
/// </summary>
public abstract class AuditableEntity : Entity
{
    protected AuditableEntity(Guid id)
        : base(id)
    {
    }

    protected AuditableEntity()
    {
    }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public void MarkCreated(DateTime utcNow) => CreatedAtUtc = utcNow;

    public void MarkUpdated(DateTime utcNow) => UpdatedAtUtc = utcNow;
}
