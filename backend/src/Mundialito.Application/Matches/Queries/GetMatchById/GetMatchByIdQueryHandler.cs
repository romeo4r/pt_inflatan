using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Queries.GetMatchById;

internal sealed class GetMatchByIdQueryHandler(IMatchReadStore readStore)
    : IQueryHandler<GetMatchByIdQuery, MatchDetailResponse>
{
    public async Task<Result<MatchDetailResponse>> Handle(GetMatchByIdQuery query, CancellationToken cancellationToken)
    {
        var match = await readStore.GetByIdAsync(query.Id, cancellationToken);

        return match is null ? MatchErrors.NotFound(query.Id) : match;
    }
}
