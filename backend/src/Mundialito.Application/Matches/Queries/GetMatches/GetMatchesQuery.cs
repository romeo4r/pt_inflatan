using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Queries.GetMatches;

/// <summary>Paged list of matches filtered by date range, team, status and match day.</summary>
public sealed record GetMatchesQuery : IQuery<PagedResult<MatchResponse>>, IPagedQuery
{
    public static readonly string[] SortableFields =
        ["scheduledAt", "matchDay", "status", "homeTeam", "awayTeam", "createdAt"];

    /// <summary>Inclusive lower bound on the scheduled date (UTC).</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Inclusive upper bound on the scheduled date (UTC).</summary>
    public DateTime? ToUtc { get; init; }

    /// <summary>Matches where the team plays as home or away.</summary>
    public Guid? TeamId { get; init; }

    public MatchStatus? Status { get; init; }

    public int? MatchDay { get; init; }

    public int PageNumber { get; init; } = PagingDefaults.DefaultPageNumber;

    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;

    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Asc;
}
