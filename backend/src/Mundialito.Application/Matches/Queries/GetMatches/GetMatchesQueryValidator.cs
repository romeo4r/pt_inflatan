using FluentValidation;
using Mundialito.Application.Common.Pagination;

namespace Mundialito.Application.Matches.Queries.GetMatches;

internal sealed class GetMatchesQueryValidator : PagedQueryValidator<GetMatchesQuery>
{
    public GetMatchesQueryValidator()
        : base(GetMatchesQuery.SortableFields)
    {
        RuleFor(q => q.ToUtc)
            .GreaterThanOrEqualTo(q => q.FromUtc)
            .When(q => q.FromUtc is not null && q.ToUtc is not null)
            .WithMessage("La fecha final no puede ser anterior a la fecha inicial.");

        RuleFor(q => q.Status)
            .IsInEnum()
            .When(q => q.Status is not null)
            .WithMessage("El estado del partido no es válido.");

        RuleFor(q => q.MatchDay)
            .GreaterThanOrEqualTo(1)
            .When(q => q.MatchDay is not null)
            .WithMessage("La jornada debe ser mayor o igual a 1.");
    }
}
