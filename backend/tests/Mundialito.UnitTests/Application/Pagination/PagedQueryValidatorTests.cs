using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using SortDirection = Mundialito.Application.Common.Pagination.SortDirection;

namespace Mundialito.UnitTests.Application.Pagination;

public class PagedQueryValidatorTests
{
    private static readonly string[] SortableFields = ["name", "code"];

    private readonly SampleQueryValidator _validator = new();

    [Fact]
    public void Validate_WithDefaults_ShouldPass()
    {
        var result = _validator.Validate(new SampleQuery());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSortByInWhitelist_ShouldPassIgnoringCase()
    {
        var result = _validator.Validate(new SampleQuery { SortBy = "NAME" });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSortByOutsideWhitelist_ShouldFail()
    {
        var result = _validator.Validate(new SampleQuery { SortBy = "id; DROP TABLE Teams" });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(IPagedQuery.SortBy));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, PagingDefaults.MaxPageSize + 1)]
    public void Validate_WithPageOutOfBounds_ShouldFail(int pageNumber, int pageSize)
    {
        var result = _validator.Validate(new SampleQuery { PageNumber = pageNumber, PageSize = pageSize });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithUndefinedSortDirection_ShouldFail()
    {
        var result = _validator.Validate(new SampleQuery { SortDirection = (SortDirection)7 });

        result.IsValid.ShouldBeFalse();
    }

    private sealed record SampleQuery : IQuery<int>, IPagedQuery
    {
        public int PageNumber { get; init; } = PagingDefaults.DefaultPageNumber;

        public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;

        public string? SortBy { get; init; }

        public SortDirection SortDirection { get; init; }
    }

    private sealed class SampleQueryValidator() : PagedQueryValidator<SampleQuery>(SortableFields);
}
