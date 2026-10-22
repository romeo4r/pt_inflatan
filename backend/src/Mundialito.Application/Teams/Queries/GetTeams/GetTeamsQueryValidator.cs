using FluentValidation;
using Mundialito.Application.Common.Pagination;

namespace Mundialito.Application.Teams.Queries.GetTeams;

internal sealed class GetTeamsQueryValidator : PagedQueryValidator<GetTeamsQuery>
{
    public const int SearchMaxLength = 100;

    public GetTeamsQueryValidator()
        : base(GetTeamsQuery.SortableFields)
    {
        RuleFor(q => q.Search)
            .MaximumLength(SearchMaxLength)
            .WithMessage($"El texto de búsqueda no puede superar {SearchMaxLength} caracteres.");
    }
}
