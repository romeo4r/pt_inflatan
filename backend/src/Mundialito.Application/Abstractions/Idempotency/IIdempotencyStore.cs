namespace Mundialito.Application.Abstractions.Idempotency;

/// <summary>What the API must send back when a request is replayed.</summary>
public sealed record IdempotentResponse(int StatusCode, string? ContentType, string Body, string? Location);

public enum ReservationOutcome
{
    /// <summary>First time this key is seen: the caller owns it and must complete or release it.</summary>
    Reserved,

    /// <summary>Same key and same payload already processed: replay <see cref="IdempotencyReservation.Response"/>.</summary>
    Completed,

    /// <summary>Same key is being processed by another request right now.</summary>
    InProgress,

    /// <summary>Same key but a different payload: the client is reusing a key incorrectly.</summary>
    PayloadMismatch,
}

public sealed record IdempotencyReservation(ReservationOutcome Outcome, IdempotentResponse? Response = null);

/// <summary>
/// Durable record of processed Idempotency-Keys. Reservation is atomic (a primary key insert),
/// so two concurrent requests with the same key cannot both execute the handler.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyReservation> TryReserveAsync(
        string key,
        string requestHash,
        string method,
        string path,
        CancellationToken cancellationToken);

    Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken);

    /// <summary>Frees the key after a failure so the client can retry with it.</summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken);
}
