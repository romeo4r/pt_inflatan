using Mundialito.Application.Abstractions.Persistence;

namespace Mundialito.Infrastructure.Persistence.DomainEvents;

/// <summary>
/// Tracks audit rows on the request's DbContext. The Unit of Work saves them in the dispatch
/// round that follows the handler, inside the same transaction as the business change.
/// </summary>
internal sealed class DomainEventLogRepository(ApplicationDbContext context) : IDomainEventLogRepository
{
    public void Add(DomainEventLogEntry entry) => context.DomainEventLogs.Add(DomainEventLogRecord.From(entry));
}
