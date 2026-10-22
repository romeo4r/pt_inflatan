using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Domain.Common;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Queries.GetTeamById;

internal sealed class GetTeamByIdQueryHandler(ITeamReadStore readStore)
    : IQueryHandler<GetTeamByIdQuery, TeamResponse>
{
    public async Task<Result<TeamResponse>> Handle(GetTeamByIdQuery query, CancellationToken cancellationToken)
    {
        var team = await readStore.GetByIdAsync(query.Id, cancellationToken);

        return team is null ? TeamErrors.NotFound(query.Id) : team;
    }
}
