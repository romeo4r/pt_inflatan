using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Domain.Common;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Queries.GetPlayerById;

internal sealed class GetPlayerByIdQueryHandler(IPlayerReadStore readStore)
    : IQueryHandler<GetPlayerByIdQuery, PlayerResponse>
{
    public async Task<Result<PlayerResponse>> Handle(GetPlayerByIdQuery query, CancellationToken cancellationToken)
    {
        var player = await readStore.GetByIdAsync(query.Id, cancellationToken);

        return player is null ? PlayerErrors.NotFound(query.Id) : player;
    }
}
