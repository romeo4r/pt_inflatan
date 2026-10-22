using Mundialito.Application.Common.Pagination;

namespace Mundialito.Infrastructure.ReadModel;

/// <summary>
/// Small building blocks shared by the Dapper read stores. Everything that ends up inside the SQL
/// text (column names, sort direction) comes from code-owned whitelists; user input only ever
/// travels as a parameter.
/// </summary>
internal static class SqlQueryHelpers
{
    private const char LikeEscapeChar = '\\';

    /// <summary>
    /// Builds an ORDER BY clause from a whitelisted sort key. <paramref name="columnMap"/> maps the
    /// public sort key (e.g. "name") to a SQL expression (e.g. "t.Name"); the tie-breaker keeps
    /// paging deterministic when the sort column has duplicates.
    /// </summary>
    public static string BuildOrderBy(
        IPagedQuery query,
        IReadOnlyDictionary<string, string> columnMap,
        string defaultSortKey,
        string tieBreaker)
    {
        var sortKey = string.IsNullOrWhiteSpace(query.SortBy) ? defaultSortKey : query.SortBy;

        if (!columnMap.TryGetValue(sortKey, out var column))
        {
            // Validators reject unknown keys before reaching here; this is a defensive guard.
            throw new ArgumentException($"Sort key '{sortKey}' is not allowed.", nameof(query));
        }

        var direction = query.SortDirection == SortDirection.Desc ? "DESC" : "ASC";

        return $"ORDER BY {column} {direction}, {tieBreaker}";
    }

    public static int Offset(IPagedQuery query) => (query.PageNumber - 1) * query.PageSize;

    /// <summary>
    /// Turns free text into a LIKE pattern that matches anywhere in the column, escaping the LIKE
    /// wildcards so a search for "100%" does not become a wildcard. Use with <c>ESCAPE '\'</c>.
    /// </summary>
    public static string? ContainsPattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var escaped = search.Trim()
            .Replace(LikeEscapeChar.ToString(), $"{LikeEscapeChar}{LikeEscapeChar}", StringComparison.Ordinal)
            .Replace("%", $"{LikeEscapeChar}%", StringComparison.Ordinal)
            .Replace("_", $"{LikeEscapeChar}_", StringComparison.Ordinal)
            .Replace("[", $"{LikeEscapeChar}[", StringComparison.Ordinal);

        return $"%{escaped}%";
    }

    /// <summary>Case-insensitive map so "createdAt" and "CreatedAt" resolve to the same column.</summary>
    public static IReadOnlyDictionary<string, string> ColumnMap(params (string Key, string Column)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Column, StringComparer.OrdinalIgnoreCase);
}
