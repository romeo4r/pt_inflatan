using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;

namespace Mundialito.Application.Teams.Queries.GetTeams;

/// <summary>Paged list of teams with optional text search and active filter.</summary>
public sealed record GetTeamsQuery : IQuery<PagedResult<TeamResponse>>, IPagedQuery
{
    /// <summary>Allowed values for <see cref="SortBy"/>; the read store maps each one to a column.</summary>
    public static readonly string[] SortableFields = ["name", "code", "isActive", "playerCount", "createdAt"];

    /// <summary>Matches teams whose name or code contains this text.</summary>
    public string? Search { get; init; }

    public bool? IsActive { get; init; }

    public int PageNumber { get; init; } = PagingDefaults.DefaultPageNumber;

    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;

    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Asc;
}
