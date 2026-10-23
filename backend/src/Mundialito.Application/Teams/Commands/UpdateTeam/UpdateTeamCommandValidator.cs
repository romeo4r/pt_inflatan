using FluentValidation;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.UpdateTeam;

internal sealed class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty().WithMessage("El identificador del equipo es obligatorio.");

        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("El nombre del equipo es obligatorio.")
            .MaximumLength(Team.NameMaxLength).WithMessage($"El nombre no puede superar {Team.NameMaxLength} caracteres.");

        RuleFor(c => c.Code)
            .NotEmpty().WithMessage("El código del equipo es obligatorio.")
            .Length(Team.CodeMinLength, Team.CodeMaxLength)
                .WithMessage($"El código debe tener entre {Team.CodeMinLength} y {Team.CodeMaxLength} caracteres.")
            .Matches("^[A-Za-z0-9]+$").WithMessage("El código solo admite letras y números.");

        RuleFor(c => c.Coach)
            .MaximumLength(Team.CoachMaxLength)
            .WithMessage($"El nombre del entrenador no puede superar {Team.CoachMaxLength} caracteres.");
    }
}
