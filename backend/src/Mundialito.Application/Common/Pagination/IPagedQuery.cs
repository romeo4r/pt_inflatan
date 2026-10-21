namespace Mundialito.Application.Common.Pagination;

/// <summary>
/// Paging and sorting inputs shared by every list query. The read side applies them in SQL
/// (OFFSET/FETCH), never in memory.
/// </summary>
public interface IPagedQuery
{
    int PageNumber { get; }

    int PageSize { get; }

    /// <summary>Property name to sort by. Each query validates it against a whitelist.</summary>
    string? SortBy { get; }

    SortDirection SortDirection { get; }
}

public static class PagingDefaults
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
}
