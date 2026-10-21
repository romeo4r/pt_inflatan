namespace Mundialito.Application.Common.Pagination;

/// <summary>
/// Page of results with the metadata required by the API contract:
/// data, pageNumber, pageSize, totalRecords and totalPages.
/// </summary>
public sealed record PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> data, int pageNumber, int pageSize, int totalRecords)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalRecords);

        Data = data;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalRecords = totalRecords;
    }

    public IReadOnlyList<T> Data { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalRecords { get; }

    public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResult<T> Empty(int pageNumber, int pageSize) => new([], pageNumber, pageSize, 0);
}
