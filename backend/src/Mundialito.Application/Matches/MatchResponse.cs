using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches;

/// <summary>Read model for a fixture row, with both teams denormalized.</summary>
public sealed record MatchResponse(
    Guid Id,
    int MatchDay,
    DateTime ScheduledAtUtc,
    string? Venue,
    MatchStatus Status,
    Guid HomeTeamId,
    string HomeTeamName,
    string HomeTeamCode,
    Guid AwayTeamId,
    string AwayTeamName,
    string AwayTeamCode,
    int? HomeScore,
    int? AwayScore,
    DateTime? FinishedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[]? RowVersion);

/// <summary>Single match with its goals. Returned by the detail endpoint only.</summary>
public sealed record MatchDetailResponse(MatchResponse Match, IReadOnlyList<GoalResponse> Goals);

public sealed record GoalResponse(
    Guid Id,
    Guid PlayerId,
    string PlayerFirstName,
    string PlayerLastName,
    Guid TeamId,
    string TeamCode,
    int? Minute)
{
    public string PlayerFullName => $"{PlayerFirstName} {PlayerLastName}";
}
