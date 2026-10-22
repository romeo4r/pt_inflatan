using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using Mundialito.Infrastructure.ReadModel;
using SortDirection = Mundialito.Application.Common.Pagination.SortDirection;

namespace Mundialito.UnitTests.Infrastructure.ReadModel;

public class SqlQueryHelpersTests
{
    private static readonly IReadOnlyDictionary<string, string> Columns = SqlQueryHelpers.ColumnMap(
        ("name", "t.Name"),
        ("createdAt", "t.CreatedAtUtc"));

    [Fact]
    public void BuildOrderBy_WithoutSortBy_ShouldUseDefaultAscending()
    {
        var orderBy = SqlQueryHelpers.BuildOrderBy(new SampleQuery(), Columns, "name", "t.Id");

        orderBy.ShouldBe("ORDER BY t.Name ASC, t.Id");
    }

    [Fact]
    public void BuildOrderBy_ShouldResolveKeyIgnoringCaseAndApplyDirection()
    {
        var query = new SampleQuery { SortBy = "CREATEDAT", SortDirection = SortDirection.Desc };

        var orderBy = SqlQueryHelpers.BuildOrderBy(query, Columns, "name", "t.Id");

        orderBy.ShouldBe("ORDER BY t.CreatedAtUtc DESC, t.Id");
    }

    [Fact]
    public void BuildOrderBy_WithUnknownKey_ShouldThrowInsteadOfInterpolating()
    {
        var query = new SampleQuery { SortBy = "name; DROP TABLE Teams --" };

        Should.Throw<ArgumentException>(() => SqlQueryHelpers.BuildOrderBy(query, Columns, "name", "t.Id"));
    }

    [Theory]
    [InlineData(1, 10, 0)]
    [InlineData(2, 10, 10)]
    [InlineData(4, 25, 75)]
    public void Offset_ShouldSkipPreviousPages(int pageNumber, int pageSize, int expected)
    {
        SqlQueryHelpers.Offset(new SampleQuery { PageNumber = pageNumber, PageSize = pageSize }).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("fin", "%fin%")]
    [InlineData(" fin ", "%fin%")]
    [InlineData("100%", @"%100\%%")]
    [InlineData("a_b", @"%a\_b%")]
    [InlineData("[x]", @"%\[x]%")]
    [InlineData(@"a\b", @"%a\\b%")]
    public void ContainsPattern_ShouldEscapeLikeWildcards(string? input, string? expected)
    {
        SqlQueryHelpers.ContainsPattern(input).ShouldBe(expected);
    }

    private sealed record SampleQuery : IQuery<int>, IPagedQuery
    {
        public int PageNumber { get; init; } = 1;

        public int PageSize { get; init; } = 10;

        public string? SortBy { get; init; }

        public SortDirection SortDirection { get; init; }
    }
}
