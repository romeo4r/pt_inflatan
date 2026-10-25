using Dapper;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.DomainEvents;
using Mundialito.Application.DomainEvents.Queries.GetDomainEvents;

namespace Mundialito.Infrastructure.ReadModel;

internal sealed class DomainEventReadStore(IDbConnectionFactory connectionFactory) : IDomainEventReadStore
{
    private static readonly IReadOnlyDictionary<string, string> SortColumns = SqlQueryHelpers.ColumnMap(
        ("occurredAt", "e.OccurredAtUtc"),
        ("eventType", "e.EventType"));

    private const string SelectColumns = """
        SELECT
            e.Id,
            e.EventType,
            e.AggregateId,
            e.Payload,
            e.CorrelationId,
            e.TraceId,
            e.OccurredAtUtc,
            e.RecordedAtUtc
        FROM DomainEventLogs e
        """;

    private const string Filter = """
        WHERE (@EventType IS NULL OR e.EventType = @EventType)
          AND (@AggregateId IS NULL OR e.AggregateId = @AggregateId)
          AND (@CorrelationId IS NULL OR e.CorrelationId = @CorrelationId)
        """;

    public async Task<PagedResult<DomainEventLogResponse>> GetPagedAsync(GetDomainEventsQuery query, CancellationToken cancellationToken)
    {
        var orderBy = SqlQueryHelpers.BuildOrderBy(query, SortColumns, defaultSortKey: "occurredAt", tieBreaker: "e.Id");

        var sql = $"""
            {SelectColumns}
            {Filter}
            {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM DomainEventLogs e
            {Filter};
            """;

        var parameters = new
        {
            EventType = string.IsNullOrWhiteSpace(query.EventType) ? null : query.EventType.Trim(),
            query.AggregateId,
            CorrelationId = string.IsNullOrWhiteSpace(query.CorrelationId) ? null : query.CorrelationId.Trim(),
            Offset = SqlQueryHelpers.Offset(query),
            query.PageSize,
        };

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var data = (await multi.ReadAsync<DomainEventLogResponse>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<DomainEventLogResponse>(data, query.PageNumber, query.PageSize, total);
    }
}
