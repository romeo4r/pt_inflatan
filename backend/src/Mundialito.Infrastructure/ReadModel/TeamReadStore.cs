using Dapper;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Teams;
using Mundialito.Application.Teams.Queries.GetTeams;

namespace Mundialito.Infrastructure.ReadModel;

internal sealed class TeamReadStore(IDbConnectionFactory connectionFactory) : ITeamReadStore
{
    private static readonly IReadOnlyDictionary<string, string> SortColumns = SqlQueryHelpers.ColumnMap(
        ("name", "t.Name"),
        ("code", "t.Code"),
        ("isActive", "t.IsActive"),
        ("playerCount", "PlayerCount"),
        ("createdAt", "t.CreatedAtUtc"));

    private const string SelectColumns = """
        SELECT
            t.Id,
            t.Name,
            t.Code,
            t.Coach,
            t.IsActive,
            (SELECT COUNT(*) FROM Players p WHERE p.TeamId = t.Id AND p.IsActive = 1) AS PlayerCount,
            t.CreatedAtUtc,
            t.UpdatedAtUtc,
            t.RowVersion
        FROM Teams t
        """;

    private const string Filter = """
        WHERE (@SearchPattern IS NULL OR t.Name LIKE @SearchPattern ESCAPE '\' OR t.Code LIKE @SearchPattern ESCAPE '\')
          AND (@IsActive IS NULL OR t.IsActive = @IsActive)
        """;

    public async Task<PagedResult<TeamResponse>> GetPagedAsync(GetTeamsQuery query, CancellationToken cancellationToken)
    {
        var orderBy = SqlQueryHelpers.BuildOrderBy(query, SortColumns, defaultSortKey: "name", tieBreaker: "t.Id");

        var sql = $"""
            {SelectColumns}
            {Filter}
            {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM Teams t
            {Filter};
            """;

        var parameters = new
        {
            SearchPattern = SqlQueryHelpers.ContainsPattern(query.Search),
            query.IsActive,
            Offset = SqlQueryHelpers.Offset(query),
            query.PageSize,
        };

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var data = (await multi.ReadAsync<TeamResponse>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<TeamResponse>(data, query.PageNumber, query.PageSize, total);
    }

    public async Task<TeamResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = $"""
            {SelectColumns}
            WHERE t.Id = @Id;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<TeamResponse>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
