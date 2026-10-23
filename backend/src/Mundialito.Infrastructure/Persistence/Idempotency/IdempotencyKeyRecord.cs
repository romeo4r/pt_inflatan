namespace Mundialito.Infrastructure.Persistence.Idempotency;

/// <summary>
/// One row per Idempotency-Key. The row is inserted when the request is first seen (pending,
/// no response yet) and completed with the response once the handler finishes.
/// </summary>
public sealed class IdempotencyKeyRecord
{
    public const int KeyMaxLength = 128;
    public const int HashMaxLength = 64;
    public const int MethodMaxLength = 10;
    public const int PathMaxLength = 512;
    public const int ContentTypeMaxLength = 100;

    public required string Key { get; init; }

    /// <summary>SHA-256 (hex) of method + path + body; detects a key reused with another payload.</summary>
    public required string RequestHash { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public int? ResponseStatusCode { get; set; }

    public string? ResponseContentType { get; set; }

    public string? ResponseBody { get; set; }

    public string? ResponseLocation { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public bool IsCompleted => ResponseStatusCode is not null;
}
