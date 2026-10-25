using Dapper;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Tournament;
using Mundialito.Application.Tournament.Queries.GetTopScorers;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Tournament;

namespace Mundialito.Infrastructure.ReadModel;

internal sealed class TournamentReadStore(IDbConnectionFactory connectionFactory) : ITournamentReadStore
{
    private static readonly int FinishedStatus = (int)MatchStatus.Finished;

    /// <summary>
    /// Active teams always appear; an inactive team keeps its line while it has finished matches,
    /// so the table never loses points that were actually played.
    /// </summary>
    private const string StandingsSql = """
        SELECT t.Id, t.Name, t.Code
        FROM Teams t
        WHERE t.IsActive = 1
           OR EXISTS (SELECT 1 FROM Matches m WHERE m.Status = @Finished AND (m.HomeTeamId = t.Id OR m.AwayTeamId = t.Id))
        ORDER BY t.Name;

        SELECT m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore
        FROM Matches m
        WHERE m.Status = @Finished;
        """;

    /// <summary>
    /// One aggregated query: goals per player, dense-ranked by total, paged in the database.
    /// Only goals from finished matches count (goals cannot exist otherwise, but the join is the guard).
    /// </summary>
    private const string TopScorersSql = """
        SELECT
            CAST(DENSE_RANK() OVER (ORDER BY COUNT(*) DESC) AS int) AS Rank,
            p.Id AS PlayerId,
            p.FirstName,
            p.LastName,
            p.JerseyNumber,
            t.Id AS TeamId,
            t.Name AS TeamName,
            t.Code AS TeamCode,
            COUNT(*) AS Goals,
            COUNT(DISTINCT g.MatchId) AS MatchesScoredIn
        FROM Goals g
        INNER JOIN Matches m ON m.Id = g.MatchId
        INNER JOIN Players p ON p.Id = g.PlayerId
        INNER JOIN Teams t ON t.Id = g.TeamId
        WHERE m.Status = @Finished
          AND (@TeamId IS NULL OR g.TeamId = @TeamId)
        GROUP BY p.Id, p.FirstName, p.LastName, p.JerseyNumber, t.Id, t.Name, t.Code
        ORDER BY Goals DESC, p.LastName, p.FirstName, p.Id
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

        SELECT COUNT(*)
        FROM (
            SELECT g.PlayerId
            FROM Goals g
            INNER JOIN Matches m ON m.Id = g.MatchId
            WHERE m.Status = @Finished
              AND (@TeamId IS NULL OR g.TeamId = @TeamId)
            GROUP BY g.PlayerId
        ) AS scorers;
        """;

    public async Task<StandingsSource> GetStandingsSourceAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(StandingsSql, new { Finished = FinishedStatus }, cancellationToken: cancellationToken));

        var teams = (await multi.ReadAsync<TournamentTeam>()).ToList();
        var matches = (await multi.ReadAsync<PlayedMatch>()).ToList();

        return new StandingsSource(teams, matches);
    }

    public async Task<PagedResult<TopScorerResponse>> GetTopScorersAsync(GetTopScorersQuery query, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            Finished = FinishedStatus,
            query.TeamId,
            Offset = SqlQueryHelpers.Offset(query),
            query.PageSize,
        };

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(TopScorersSql, parameters, cancellationToken: cancellationToken));

        var data = (await multi.ReadAsync<TopScorerResponse>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<TopScorerResponse>(data, query.PageNumber, query.PageSize, total);
    }
}
