using Dapper;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Players;
using Mundialito.Application.Players.Queries.GetPlayers;

namespace Mundialito.Infrastructure.ReadModel;

internal sealed class PlayerReadStore(IDbConnectionFactory connectionFactory) : IPlayerReadStore
{
    private static readonly IReadOnlyDictionary<string, string> SortColumns = SqlQueryHelpers.ColumnMap(
        ("lastName", "p.LastName"),
        ("firstName", "p.FirstName"),
        ("jerseyNumber", "p.JerseyNumber"),
        ("position", "p.Position"),
        ("teamName", "t.Name"),
        ("isActive", "p.IsActive"),
        ("createdAt", "p.CreatedAtUtc"));

    private const string SelectColumns = """
        SELECT
            p.Id,
            p.TeamId,
            t.Name AS TeamName,
            t.Code AS TeamCode,
            p.FirstName,
            p.LastName,
            p.JerseyNumber,
            p.Position,
            p.BirthDate,
            p.IsActive,
            p.CreatedAtUtc,
            p.UpdatedAtUtc,
            p.RowVersion
        FROM Players p
        INNER JOIN Teams t ON t.Id = p.TeamId
        """;

    private const string Filter = """
        WHERE (@TeamId IS NULL OR p.TeamId = @TeamId)
          AND (@SearchPattern IS NULL OR p.FirstName LIKE @SearchPattern ESCAPE '\' OR p.LastName LIKE @SearchPattern ESCAPE '\')
          AND (@Position IS NULL OR p.Position = @Position)
          AND (@IsActive IS NULL OR p.IsActive = @IsActive)
        """;

    public async Task<PagedResult<PlayerResponse>> GetPagedAsync(GetPlayersQuery query, CancellationToken cancellationToken)
    {
        var orderBy = SqlQueryHelpers.BuildOrderBy(query, SortColumns, defaultSortKey: "lastName", tieBreaker: "p.FirstName, p.Id");

        var sql = $"""
            {SelectColumns}
            {Filter}
            {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM Players p
            {Filter};
            """;

        var parameters = new
        {
            query.TeamId,
            SearchPattern = SqlQueryHelpers.ContainsPattern(query.Search),
            Position = (int?)query.Position,
            query.IsActive,
            Offset = SqlQueryHelpers.Offset(query),
            query.PageSize,
        };

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var data = (await multi.ReadAsync<PlayerResponse>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<PlayerResponse>(data, query.PageNumber, query.PageSize, total);
    }

    public async Task<PlayerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = $"""
            {SelectColumns}
            WHERE p.Id = @Id;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<PlayerResponse>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
