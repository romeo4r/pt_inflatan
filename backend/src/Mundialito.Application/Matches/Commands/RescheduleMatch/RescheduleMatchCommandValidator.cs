using FluentValidation;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Commands.RescheduleMatch;

internal sealed class RescheduleMatchCommandValidator : AbstractValidator<RescheduleMatchCommand>
{
    public RescheduleMatchCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty().WithMessage("El identificador del partido es obligatorio.");

        RuleFor(c => c.MatchDay)
            .GreaterThanOrEqualTo(1).WithMessage("La jornada debe ser mayor o igual a 1.");

        RuleFor(c => c.ScheduledAtUtc)
            .NotEqual(default(DateTime)).WithMessage("La fecha y hora del partido son obligatorias.");

        RuleFor(c => c.Venue)
            .MaximumLength(Match.VenueMaxLength).WithMessage($"La sede no puede superar {Match.VenueMaxLength} caracteres.");
    }
}
