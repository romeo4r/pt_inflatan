using Mundialito.Application.Common.Pagination;

namespace Mundialito.Api.Common;

/// <summary>Query-string helpers shared by every paged list endpoint.</summary>
public static class PagingRequest
{
    /// <summary>Sentinel outside the enum so the validator reports a clean "asc or desc" message.</summary>
    private const SortDirection InvalidSortDirection = (SortDirection)(-1);

    /// <summary>Accepts "asc"/"desc" in any casing; null means ascending.</summary>
    public static SortDirection ParseSortDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SortDirection.Asc;
        }

        return Enum.TryParse<SortDirection>(value, ignoreCase: true, out var direction) && Enum.IsDefined(direction)
            ? direction
            : InvalidSortDirection;
    }
}
