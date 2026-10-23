using Mundialito.Domain.Players;

namespace Mundialito.Application.Players;

/// <summary>Read model for a player, denormalized with its team for list screens.</summary>
public sealed record PlayerResponse(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string TeamCode,
    string FirstName,
    string LastName,
    int JerseyNumber,
    PlayerPosition Position,
    DateOnly? BirthDate,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[]? RowVersion)
{
    public string FullName => $"{FirstName} {LastName}";
}
