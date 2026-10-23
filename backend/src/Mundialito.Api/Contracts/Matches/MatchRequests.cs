using Mundialito.Api.Common;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Matches.Commands.PatchMatch;
using Mundialito.Application.Matches.Commands.RescheduleMatch;
using Mundialito.Application.Matches.Commands.ScheduleMatch;
using Mundialito.Application.Matches.Queries.GetMatches;
using Mundialito.Domain.Matches;

namespace Mundialito.Api.Contracts.Matches;

/// <summary>Query string of GET /api/matches: date range, team (home or away), status and match day.</summary>
public sealed record GetMatchesRequest(
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? TeamId,
    string? Status,
    int? MatchDay,
    int? PageNumber,
    int? PageSize,
    string? SortBy,
    string? SortDirection)
{
    /// <summary>Sentinel outside the enum so the validator reports a clean message for bad input.</summary>
    private const MatchStatus InvalidStatus = (MatchStatus)(-1);

    public GetMatchesQuery ToQuery() => new()
    {
        FromUtc = UtcDateTime.Normalize(FromUtc),
        ToUtc = UtcDateTime.Normalize(ToUtc),
        TeamId = TeamId,
        Status = ParseStatus(Status),
        MatchDay = MatchDay,
        PageNumber = PageNumber ?? PagingDefaults.DefaultPageNumber,
        PageSize = PageSize ?? PagingDefaults.DefaultPageSize,
        SortBy = SortBy,
        SortDirection = PagingRequest.ParseSortDirection(SortDirection),
    };

    private static MatchStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<MatchStatus>(value, ignoreCase: true, out var status) && Enum.IsDefined(status)
            ? status
            : InvalidStatus;
    }
}

public sealed record ScheduleMatchRequest(
    Guid HomeTeamId,
    Guid AwayTeamId,
    int MatchDay,
    DateTime ScheduledAtUtc,
    string? Venue)
{
    public ScheduleMatchCommand ToCommand() =>
        new(HomeTeamId, AwayTeamId, MatchDay, UtcDateTime.Normalize(ScheduledAtUtc), Venue);
}

public sealed record RescheduleMatchRequest(int MatchDay, DateTime ScheduledAtUtc, string? Venue)
{
    public RescheduleMatchCommand ToCommand(Guid id, byte[]? expectedRowVersion) =>
        new(id, MatchDay, UtcDateTime.Normalize(ScheduledAtUtc), Venue, expectedRowVersion);
}

public sealed record PatchMatchRequest(int? MatchDay, DateTime? ScheduledAtUtc, string? Venue)
{
    public PatchMatchCommand ToCommand(Guid id, byte[]? expectedRowVersion) =>
        new(id, MatchDay, UtcDateTime.Normalize(ScheduledAtUtc), Venue, expectedRowVersion);
}
