namespace Mundialito.Application.Teams;

/// <summary>Read model for a team. Shaped for the API, not for the domain.</summary>
public sealed record TeamResponse(
    Guid Id,
    string Name,
    string Code,
    string? Coach,
    bool IsActive,
    int PlayerCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
