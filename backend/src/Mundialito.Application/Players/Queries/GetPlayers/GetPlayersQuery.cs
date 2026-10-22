using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Queries.GetPlayers;

/// <summary>Paged list of players, optionally restricted to one team.</summary>
public sealed record GetPlayersQuery : IQuery<PagedResult<PlayerResponse>>, IPagedQuery
{
    public static readonly string[] SortableFields =
        ["lastName", "firstName", "jerseyNumber", "position", "teamName", "isActive", "createdAt"];

    public Guid? TeamId { get; init; }

    /// <summary>Matches players whose first or last name contains this text.</summary>
    public string? Search { get; init; }

    public PlayerPosition? Position { get; init; }

    public bool? IsActive { get; init; }

    public int PageNumber { get; init; } = PagingDefaults.DefaultPageNumber;

    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;

    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Asc;
}
