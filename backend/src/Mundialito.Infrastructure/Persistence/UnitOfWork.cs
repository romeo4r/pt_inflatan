using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Common;

namespace Mundialito.Infrastructure.Persistence;

/// <summary>
/// The only place where changes reach the database. One explicit transaction wraps:
/// audit stamping, SaveChanges, domain event dispatch (handlers may persist more rows)
/// and the final commit. Any failure rolls everything back, including whatever handlers wrote.
/// Concurrency and uniqueness rejections come back as <see cref="Result"/> failures (409);
/// everything else is an infrastructure fault and propagates as an exception.
/// </summary>
internal sealed partial class UnitOfWork(
    ApplicationDbContext context,
    IDomainEventDispatcher domainEventDispatcher,
    IClock clock,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    /// <summary>Guards against handlers that keep raising events on each other forever.</summary>
    private const int MaxDispatchRounds = 10;

    public async Task<Result<int>> CommitAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            StampAuditFields();

            var affectedRows = await context.SaveChangesAsync(cancellationToken);

            affectedRows += await DispatchDomainEventsAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            LogCommitted(logger, affectedRows);

            return affectedRows;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            LogConcurrencyConflict(logger);
            return PersistenceErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (DbExceptionClassifier.IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            LogUniqueViolation(logger);
            return PersistenceErrors.UniqueViolation;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            LogRolledBack(logger, exception);
            throw;
        }
    }

    private void StampAuditFields()
    {
        var now = clock.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.MarkCreated(now);
                    break;
                case EntityState.Modified:
                    entry.Entity.MarkUpdated(now);
                    break;
            }
        }
    }

    /// <summary>
    /// Collects the events raised by tracked aggregates, clears them so they are delivered once,
    /// and dispatches them. Handlers may add entities, so changes are saved again after each round.
    /// </summary>
    private async Task<int> DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var affectedRows = 0;

        for (var round = 0; round < MaxDispatchRounds; round++)
        {
            var domainEvents = context.ChangeTracker
                .Entries<Entity>()
                .Select(entry => entry.Entity)
                .Where(entity => entity.DomainEvents.Count > 0)
                .SelectMany(entity =>
                {
                    var events = entity.DomainEvents.ToArray();
                    entity.ClearDomainEvents();
                    return events;
                })
                .ToArray();

            if (domainEvents.Length == 0)
            {
                return affectedRows;
            }

            await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

            if (context.ChangeTracker.HasChanges())
            {
                StampAuditFields();
                affectedRows += await context.SaveChangesAsync(cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Domain event dispatch did not settle after {MaxDispatchRounds} rounds; check for handlers raising events recursively.");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unit of work committed {AffectedRows} rows")]
    private static partial void LogCommitted(ILogger logger, int affectedRows);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unit of work rolled back: optimistic concurrency conflict")]
    private static partial void LogConcurrencyConflict(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unit of work rolled back: unique constraint violation")]
    private static partial void LogUniqueViolation(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unit of work rolled back")]
    private static partial void LogRolledBack(ILogger logger, Exception exception);
}
