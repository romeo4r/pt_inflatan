using Dapper;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Matches;
using Mundialito.Application.Matches.Queries.GetMatches;

namespace Mundialito.Infrastructure.ReadModel;

internal sealed class MatchReadStore(IDbConnectionFactory connectionFactory) : IMatchReadStore
{
    private static readonly IReadOnlyDictionary<string, string> SortColumns = SqlQueryHelpers.ColumnMap(
        ("scheduledAt", "m.ScheduledAtUtc"),
        ("matchDay", "m.MatchDay"),
        ("status", "m.Status"),
        ("homeTeam", "ht.Name"),
        ("awayTeam", "at.Name"),
        ("createdAt", "m.CreatedAtUtc"));

    private const string SelectColumns = """
        SELECT
            m.Id,
            m.MatchDay,
            m.ScheduledAtUtc,
            m.Venue,
            m.Status,
            m.HomeTeamId,
            ht.Name AS HomeTeamName,
            ht.Code AS HomeTeamCode,
            m.AwayTeamId,
            at.Name AS AwayTeamName,
            at.Code AS AwayTeamCode,
            m.HomeScore,
            m.AwayScore,
            m.FinishedAtUtc,
            m.CreatedAtUtc,
            m.UpdatedAtUtc,
            m.RowVersion
        FROM Matches m
        INNER JOIN Teams ht ON ht.Id = m.HomeTeamId
        INNER JOIN Teams at ON at.Id = m.AwayTeamId
        """;

    private const string Filter = """
        WHERE (@FromUtc IS NULL OR m.ScheduledAtUtc >= @FromUtc)
          AND (@ToUtc IS NULL OR m.ScheduledAtUtc <= @ToUtc)
          AND (@TeamId IS NULL OR m.HomeTeamId = @TeamId OR m.AwayTeamId = @TeamId)
          AND (@Status IS NULL OR m.Status = @Status)
          AND (@MatchDay IS NULL OR m.MatchDay = @MatchDay)
        """;

    private const string GoalsSql = """
        SELECT
            g.Id,
            g.PlayerId,
            p.FirstName AS PlayerFirstName,
            p.LastName AS PlayerLastName,
            g.TeamId,
            t.Code AS TeamCode,
            g.Minute
        FROM Goals g
        INNER JOIN Players p ON p.Id = g.PlayerId
        INNER JOIN Teams t ON t.Id = g.TeamId
        WHERE g.MatchId = @Id
        ORDER BY g.Minute, g.Id;
        """;

    public async Task<PagedResult<MatchResponse>> GetPagedAsync(GetMatchesQuery query, CancellationToken cancellationToken)
    {
        var orderBy = SqlQueryHelpers.BuildOrderBy(query, SortColumns, defaultSortKey: "scheduledAt", tieBreaker: "m.Id");

        var sql = $"""
            {SelectColumns}
            {Filter}
            {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM Matches m
            {Filter};
            """;

        var parameters = new
        {
            query.FromUtc,
            query.ToUtc,
            query.TeamId,
            Status = (int?)query.Status,
            query.MatchDay,
            Offset = SqlQueryHelpers.Offset(query),
            query.PageSize,
        };

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var data = (await multi.ReadAsync<MatchResponse>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<MatchResponse>(data, query.PageNumber, query.PageSize, total);
    }

    public async Task<MatchDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = $"""
            {SelectColumns}
            WHERE m.Id = @Id;

            {GoalsSql}
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        var match = await multi.ReadSingleOrDefaultAsync<MatchResponse>();
        if (match is null)
        {
            return null;
        }

        var goals = (await multi.ReadAsync<GoalResponse>()).ToList();

        return new MatchDetailResponse(match, goals);
    }
}
