using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Tournament;
using Mundialito.Application.Tournament.Queries.GetTopScorers;
using Mundialito.Domain.Tournament;

namespace Mundialito.Application.Abstractions.ReadStores;

/// <summary>Teams and finished matches, the only inputs the standings calculator needs.</summary>
public sealed record StandingsSource(IReadOnlyList<TournamentTeam> Teams, IReadOnlyList<PlayedMatch> Matches);

/// <summary>Read side for tournament tables (Dapper).</summary>
public interface ITournamentReadStore
{
    Task<StandingsSource> GetStandingsSourceAsync(CancellationToken cancellationToken);

    /// <summary>Aggregated in SQL (GROUP BY + DENSE_RANK) and paged in the database.</summary>
    Task<PagedResult<TopScorerResponse>> GetTopScorersAsync(GetTopScorersQuery query, CancellationToken cancellationToken);
}
