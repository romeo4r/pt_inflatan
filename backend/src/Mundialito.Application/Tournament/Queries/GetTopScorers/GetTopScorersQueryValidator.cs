using Mundialito.Application.Common.Pagination;

namespace Mundialito.Application.Tournament.Queries.GetTopScorers;

internal sealed class GetTopScorersQueryValidator : PagedQueryValidator<GetTopScorersQuery>
{
    public GetTopScorersQueryValidator()
        : base(GetTopScorersQuery.SortableFields)
    {
    }
}
