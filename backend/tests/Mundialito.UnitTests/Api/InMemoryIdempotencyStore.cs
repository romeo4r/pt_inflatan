using System.Collections.Concurrent;
using Mundialito.Application.Abstractions.Idempotency;

namespace Mundialito.UnitTests.Api;

/// <summary>Dictionary-backed store so endpoint tests exercise real replay behaviour without a database.</summary>
internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, (string Hash, IdempotentResponse? Response)> _entries = new();

    public Task<IdempotencyReservation> TryReserveAsync(
        string key,
        string requestHash,
        string method,
        string path,
        CancellationToken cancellationToken)
    {
        if (_entries.TryAdd(key, (requestHash, null)))
        {
            return Task.FromResult(new IdempotencyReservation(ReservationOutcome.Reserved));
        }

        var existing = _entries[key];
        if (existing.Hash != requestHash)
        {
            return Task.FromResult(new IdempotencyReservation(ReservationOutcome.PayloadMismatch));
        }

        return Task.FromResult(existing.Response is null
            ? new IdempotencyReservation(ReservationOutcome.InProgress)
            : new IdempotencyReservation(ReservationOutcome.Completed, existing.Response));
    }

    public Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            _entries[key] = (entry.Hash, response);
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string key, CancellationToken cancellationToken)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
