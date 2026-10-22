using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Teams.Queries.GetTeams;

internal sealed class GetTeamsQueryHandler(ITeamReadStore readStore)
    : IQueryHandler<GetTeamsQuery, PagedResult<TeamResponse>>
{
    public async Task<Result<PagedResult<TeamResponse>>> Handle(GetTeamsQuery query, CancellationToken cancellationToken) =>
        await readStore.GetPagedAsync(query, cancellationToken);
}
