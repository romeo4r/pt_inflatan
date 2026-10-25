using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Tournament.Queries.GetTopScorers;

internal sealed class GetTopScorersQueryHandler(ITournamentReadStore readStore)
    : IQueryHandler<GetTopScorersQuery, PagedResult<TopScorerResponse>>
{
    public async Task<Result<PagedResult<TopScorerResponse>>> Handle(GetTopScorersQuery query, CancellationToken cancellationToken) =>
        await readStore.GetTopScorersAsync(query, cancellationToken);
}
