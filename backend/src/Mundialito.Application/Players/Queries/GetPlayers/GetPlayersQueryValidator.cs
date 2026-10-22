using FluentValidation;
using Mundialito.Application.Common.Pagination;

namespace Mundialito.Application.Players.Queries.GetPlayers;

internal sealed class GetPlayersQueryValidator : PagedQueryValidator<GetPlayersQuery>
{
    public const int SearchMaxLength = 100;

    public GetPlayersQueryValidator()
        : base(GetPlayersQuery.SortableFields)
    {
        RuleFor(q => q.Search)
            .MaximumLength(SearchMaxLength)
            .WithMessage($"El texto de búsqueda no puede superar {SearchMaxLength} caracteres.");

        RuleFor(q => q.Position)
            .IsInEnum()
            .When(q => q.Position is not null)
            .WithMessage("La posición no es válida.");
    }
}
