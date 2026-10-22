using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Matches.Queries.GetMatches;

internal sealed class GetMatchesQueryHandler(IMatchReadStore readStore)
    : IQueryHandler<GetMatchesQuery, PagedResult<MatchResponse>>
{
    public async Task<Result<PagedResult<MatchResponse>>> Handle(GetMatchesQuery query, CancellationToken cancellationToken) =>
        await readStore.GetPagedAsync(query, cancellationToken);
}
