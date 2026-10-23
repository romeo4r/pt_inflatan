using FluentValidation;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Commands.PatchMatch;

internal sealed class PatchMatchCommandValidator : AbstractValidator<PatchMatchCommand>
{
    public PatchMatchCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty().WithMessage("El identificador del partido es obligatorio.");

        RuleFor(c => c)
            .Must(c => c.HasChanges)
            .WithName("body")
            .WithMessage("Debe enviar al menos un campo para modificar.");

        When(c => c.MatchDay is not null, () =>
            RuleFor(c => c.MatchDay!.Value)
                .GreaterThanOrEqualTo(1)
                .OverridePropertyName(nameof(PatchMatchCommand.MatchDay))
                .WithMessage("La jornada debe ser mayor o igual a 1."));

        When(c => c.ScheduledAtUtc is not null, () =>
            RuleFor(c => c.ScheduledAtUtc!.Value)
                .NotEqual(default(DateTime))
                .OverridePropertyName(nameof(PatchMatchCommand.ScheduledAtUtc))
                .WithMessage("La fecha y hora del partido no son válidas."));

        RuleFor(c => c.Venue)
            .MaximumLength(Match.VenueMaxLength).WithMessage($"La sede no puede superar {Match.VenueMaxLength} caracteres.");
    }
}
