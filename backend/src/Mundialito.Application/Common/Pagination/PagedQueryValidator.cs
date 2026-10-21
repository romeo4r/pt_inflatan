using FluentValidation;

namespace Mundialito.Application.Common.Pagination;

/// <summary>
/// Base validator for list queries. Enforces page bounds and restricts <c>sortBy</c> to the
/// whitelist each query declares, which is the first line of defense against SQL injection
/// in dynamic ORDER BY clauses.
/// </summary>
public abstract class PagedQueryValidator<TQuery> : AbstractValidator<TQuery>
    where TQuery : IPagedQuery
{
    protected PagedQueryValidator(IReadOnlyCollection<string> sortableFields)
    {
        RuleFor(q => q.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("El número de página debe ser mayor o igual a 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, PagingDefaults.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {PagingDefaults.MaxPageSize}.");

        RuleFor(q => q.SortDirection)
            .IsInEnum()
            .WithMessage("La dirección de ordenamiento debe ser 'asc' o 'desc'.");

        RuleFor(q => q.SortBy)
            .Must(sortBy => sortBy is null || sortableFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"El campo de ordenamiento debe ser uno de: {string.Join(", ", sortableFields)}.");
    }
}
