namespace Mundialito.Application.Teams;

/// <summary>
/// Read model for a team. <paramref name="RowVersion"/> is the concurrency token the client must
/// echo back (as If-Match) when updating; it serializes to JSON as a base64 string.
/// </summary>
public sealed record TeamResponse(
    Guid Id,
    string Name,
    string Code,
    string? Coach,
    bool IsActive,
    int PlayerCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[]? RowVersion);
