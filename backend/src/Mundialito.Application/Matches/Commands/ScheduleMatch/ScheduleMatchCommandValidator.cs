using FluentValidation;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Commands.ScheduleMatch;

internal sealed class ScheduleMatchCommandValidator : AbstractValidator<ScheduleMatchCommand>
{
    public ScheduleMatchCommandValidator()
    {
        RuleFor(c => c.HomeTeamId)
            .NotEmpty().WithMessage("El equipo local es obligatorio.");

        RuleFor(c => c.AwayTeamId)
            .NotEmpty().WithMessage("El equipo visitante es obligatorio.")
            .NotEqual(c => c.HomeTeamId).WithMessage("Un equipo no puede jugar contra sí mismo.");

        RuleFor(c => c.MatchDay)
            .GreaterThanOrEqualTo(1).WithMessage("La jornada debe ser mayor o igual a 1.");

        RuleFor(c => c.ScheduledAtUtc)
            .NotEqual(default(DateTime)).WithMessage("La fecha y hora del partido son obligatorias.");

        RuleFor(c => c.Venue)
            .MaximumLength(Match.VenueMaxLength).WithMessage($"La sede no puede superar {Match.VenueMaxLength} caracteres.");
    }
}
