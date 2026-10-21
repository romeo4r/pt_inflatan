using Mundialito.Application.Common.Pagination;

namespace Mundialito.UnitTests.Application.Pagination;

public class PagedResultTests
{
    [Theory]
    [InlineData(50, 10, 5)]
    [InlineData(51, 10, 6)]
    [InlineData(9, 10, 1)]
    [InlineData(0, 10, 0)]
    public void TotalPages_ShouldRoundUp(int totalRecords, int pageSize, int expectedPages)
    {
        var page = new PagedResult<int>([], 1, pageSize, totalRecords);

        page.TotalPages.ShouldBe(expectedPages);
    }

    [Fact]
    public void Navigation_ShouldReflectPosition()
    {
        var first = new PagedResult<int>([1, 2], 1, 2, 5);
        var last = new PagedResult<int>([5], 3, 2, 5);

        first.HasPreviousPage.ShouldBeFalse();
        first.HasNextPage.ShouldBeTrue();
        last.HasPreviousPage.ShouldBeTrue();
        last.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void Empty_ShouldHaveNoDataAndZeroPages()
    {
        var page = PagedResult<string>.Empty(2, 10);

        page.Data.ShouldBeEmpty();
        page.TotalRecords.ShouldBe(0);
        page.TotalPages.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 10, -1)]
    public void Constructor_WithInvalidArguments_ShouldThrow(int pageNumber, int pageSize, int totalRecords)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new PagedResult<int>([], pageNumber, pageSize, totalRecords));
    }
}
