using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Teams;
using Mundialito.Application.Teams.Queries.GetTeams;

namespace Mundialito.Application.Abstractions.ReadStores;

/// <summary>
/// Read side for teams. Implemented with Dapper in Infrastructure; never used by commands.
/// Paging, filtering and sorting happen in SQL.
/// </summary>
public interface ITeamReadStore
{
    Task<PagedResult<TeamResponse>> GetPagedAsync(GetTeamsQuery query, CancellationToken cancellationToken);

    Task<TeamResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
