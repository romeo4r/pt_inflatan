using FluentValidation;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Commands.RegisterMatchResult;

internal sealed class RegisterMatchResultCommandValidator : AbstractValidator<RegisterMatchResultCommand>
{
    public RegisterMatchResultCommandValidator()
    {
        RuleFor(c => c.MatchId)
            .NotEmpty().WithMessage("El identificador del partido es obligatorio.");

        RuleFor(c => c.HomeScore)
            .GreaterThanOrEqualTo(0).WithMessage("El marcador del equipo local no puede ser negativo.");

        RuleFor(c => c.AwayScore)
            .GreaterThanOrEqualTo(0).WithMessage("El marcador del equipo visitante no puede ser negativo.");

        RuleFor(c => c.Goals)
            .NotNull().WithMessage("La lista de goles es obligatoria (puede estar vacía).");

        // Early, cheap consistency check; the domain re-checks per team once players are resolved.
        RuleFor(c => c.Goals)
            .Must((command, goals) => goals.Count == command.HomeScore + command.AwayScore)
            .When(c => c.Goals is not null)
            .WithMessage("La cantidad de goles debe coincidir con la suma del marcador.");

        RuleForEach(c => c.Goals).ChildRules(goal =>
        {
            goal.RuleFor(g => g.PlayerId)
                .NotEmpty().WithMessage("El jugador que anota es obligatorio.");

            goal.RuleFor(g => g.Minute!.Value)
                .InclusiveBetween(Goal.MinMinute, Goal.MaxMinute)
                .When(g => g.Minute is not null)
                .OverridePropertyName(nameof(GoalInput.Minute))
                .WithMessage($"El minuto del gol debe estar entre {Goal.MinMinute} y {Goal.MaxMinute}.");
        });
    }
}
