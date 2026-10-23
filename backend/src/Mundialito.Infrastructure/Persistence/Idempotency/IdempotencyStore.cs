using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Idempotency;
using Mundialito.Application.Abstractions.Time;

namespace Mundialito.Infrastructure.Persistence.Idempotency;

/// <summary>
/// EF Core implementation. Every operation runs on its own short-lived DbContext (own scope,
/// own connection) so the reservation commits immediately and independently of the business
/// transaction that the request will open later through the Unit of Work.
/// </summary>
internal sealed class IdempotencyStore(IServiceScopeFactory scopeFactory, IClock clock) : IIdempotencyStore
{
    /// <summary>How long a key is remembered. Long enough for realistic client retries.</summary>
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<IdempotencyReservation> TryReserveAsync(
        string key,
        string requestHash,
        string method,
        string path,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await context.IdempotencyKeys.FindAsync([key], cancellationToken);
        var now = clock.UtcNow;

        if (existing is not null && existing.ExpiresAtUtc <= now)
        {
            context.IdempotencyKeys.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
            existing = null;
        }

        if (existing is not null)
        {
            return Classify(existing, requestHash);
        }

        context.IdempotencyKeys.Add(new IdempotencyKeyRecord
        {
            Key = key,
            RequestHash = requestHash,
            Method = method,
            Path = path,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(Retention),
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return new IdempotencyReservation(ReservationOutcome.Reserved);
        }
        catch (DbUpdateException exception) when (DbExceptionClassifier.IsUniqueViolation(exception))
        {
            // Lost the race against a concurrent request with the same key: report its state.
            context.ChangeTracker.Clear();
            var winner = await context.IdempotencyKeys.AsNoTracking().SingleAsync(k => k.Key == key, cancellationToken);
            return Classify(winner, requestHash);
        }
    }

    public async Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var record = await context.IdempotencyKeys.FindAsync([key], cancellationToken);
        if (record is null)
        {
            return;
        }

        record.ResponseStatusCode = response.StatusCode;
        record.ResponseContentType = response.ContentType;
        record.ResponseBody = response.Body;
        record.ResponseLocation = response.Location;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.IdempotencyKeys
            .Where(k => k.Key == key)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static IdempotencyReservation Classify(IdempotencyKeyRecord record, string requestHash)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new IdempotencyReservation(ReservationOutcome.PayloadMismatch);
        }

        if (!record.IsCompleted)
        {
            return new IdempotencyReservation(ReservationOutcome.InProgress);
        }

        return new IdempotencyReservation(
            ReservationOutcome.Completed,
            new IdempotentResponse(
                record.ResponseStatusCode!.Value,
                record.ResponseContentType,
                record.ResponseBody ?? string.Empty,
                record.ResponseLocation));
    }
}
