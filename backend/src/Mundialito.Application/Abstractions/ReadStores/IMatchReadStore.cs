using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Matches;
using Mundialito.Application.Matches.Queries.GetMatches;

namespace Mundialito.Application.Abstractions.ReadStores;

/// <summary>Read side for matches (Dapper).</summary>
public interface IMatchReadStore
{
    Task<PagedResult<MatchResponse>> GetPagedAsync(GetMatchesQuery query, CancellationToken cancellationToken);

    Task<MatchDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
