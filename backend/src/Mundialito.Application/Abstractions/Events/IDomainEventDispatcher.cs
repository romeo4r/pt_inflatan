using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Events;

/// <summary>Delivers domain events to their registered handlers. Called by the Unit of Work.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
