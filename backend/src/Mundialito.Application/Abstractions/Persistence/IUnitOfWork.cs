using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Persistence;

/// <summary>
/// Single point of commit for the write side. Repositories only track changes; nothing reaches
/// the database until <see cref="CommitAsync"/> is called. The implementation wraps the commit in
/// a transaction, stamps audit fields and dispatches the domain events collected by the aggregates.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists every tracked change atomically. Returns the number of affected rows, or a
    /// conflict error when the database rejects the write because of a concurrent change
    /// (optimistic concurrency) or a duplicate value (unique index). Infrastructure failures
    /// still throw: they are not business outcomes.
    /// </summary>
    Task<Result<int>> CommitAsync(CancellationToken cancellationToken = default);
}
