using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;

namespace Mundialito.Application.Tournament.Queries.GetTopScorers;

/// <summary>Paged top-scorers table, optionally restricted to one team. Always ordered by goals.</summary>
public sealed record GetTopScorersQuery : IQuery<PagedResult<TopScorerResponse>>, IPagedQuery
{
    public static readonly string[] SortableFields = ["goals"];

    public Guid? TeamId { get; init; }

    public int PageNumber { get; init; } = PagingDefaults.DefaultPageNumber;

    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;

    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Desc;
}
