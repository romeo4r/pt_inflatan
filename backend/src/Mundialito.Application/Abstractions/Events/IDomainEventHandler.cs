using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Events;

/// <summary>
/// Reacts to a domain event. Handlers run inside the same transaction as the change that raised
/// the event, so anything they persist (audit rows, logs) is committed or rolled back with it.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
