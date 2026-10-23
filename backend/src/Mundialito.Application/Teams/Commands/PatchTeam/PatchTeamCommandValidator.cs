using FluentValidation;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.PatchTeam;

internal sealed class PatchTeamCommandValidator : AbstractValidator<PatchTeamCommand>
{
    public PatchTeamCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty().WithMessage("El identificador del equipo es obligatorio.");

        RuleFor(c => c)
            .Must(c => c.HasChanges)
            .WithName("body")
            .WithMessage("Debe enviar al menos un campo para modificar.");

        When(c => c.Name is not null, () =>
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("El nombre del equipo no puede estar vacío.")
                .MaximumLength(Team.NameMaxLength).WithMessage($"El nombre no puede superar {Team.NameMaxLength} caracteres."));

        When(c => c.Code is not null, () =>
            RuleFor(c => c.Code)
                .NotEmpty().WithMessage("El código del equipo no puede estar vacío.")
                .Length(Team.CodeMinLength, Team.CodeMaxLength)
                    .WithMessage($"El código debe tener entre {Team.CodeMinLength} y {Team.CodeMaxLength} caracteres.")
                .Matches("^[A-Za-z0-9]+$").WithMessage("El código solo admite letras y números."));

        RuleFor(c => c.Coach)
            .MaximumLength(Team.CoachMaxLength)
            .WithMessage($"El nombre del entrenador no puede superar {Team.CoachMaxLength} caracteres.");
    }
}
