using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Players.Queries.GetPlayers;

internal sealed class GetPlayersQueryHandler(IPlayerReadStore readStore)
    : IQueryHandler<GetPlayersQuery, PagedResult<PlayerResponse>>
{
    public async Task<Result<PagedResult<PlayerResponse>>> Handle(GetPlayersQuery query, CancellationToken cancellationToken) =>
        await readStore.GetPagedAsync(query, cancellationToken);
}
