using FluentValidation;
using Mundialito.Application.Common.Pagination;

namespace Mundialito.Application.DomainEvents.Queries.GetDomainEvents;

internal sealed class GetDomainEventsQueryValidator : PagedQueryValidator<GetDomainEventsQuery>
{
    public GetDomainEventsQueryValidator()
        : base(GetDomainEventsQuery.SortableFields)
    {
        RuleFor(q => q.EventType)
            .MaximumLength(200).WithMessage("El tipo de evento no puede superar 200 caracteres.");

        RuleFor(q => q.CorrelationId)
            .MaximumLength(64).WithMessage("El identificador de correlación no puede superar 64 caracteres.");
    }
}
