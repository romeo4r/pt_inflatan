using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Players;
using Mundialito.Application.Players.Queries.GetPlayers;

namespace Mundialito.Application.Abstractions.ReadStores;

/// <summary>Read side for players (Dapper).</summary>
public interface IPlayerReadStore
{
    Task<PagedResult<PlayerResponse>> GetPagedAsync(GetPlayersQuery query, CancellationToken cancellationToken);

    Task<PlayerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
