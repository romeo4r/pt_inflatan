using FluentValidation;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Commands.PatchPlayer;

internal sealed class PatchPlayerCommandValidator : AbstractValidator<PatchPlayerCommand>
{
    public PatchPlayerCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty().WithMessage("El identificador del jugador es obligatorio.");

        RuleFor(c => c)
            .Must(c => c.HasChanges)
            .WithName("body")
            .WithMessage("Debe enviar al menos un campo para modificar.");

        When(c => c.FirstName is not null, () =>
            RuleFor(c => c.FirstName)
                .NotEmpty().WithMessage("El nombre del jugador no puede estar vacío.")
                .MaximumLength(Player.NameMaxLength).WithMessage($"El nombre no puede superar {Player.NameMaxLength} caracteres."));

        When(c => c.LastName is not null, () =>
            RuleFor(c => c.LastName)
                .NotEmpty().WithMessage("El apellido del jugador no puede estar vacío.")
                .MaximumLength(Player.NameMaxLength).WithMessage($"El apellido no puede superar {Player.NameMaxLength} caracteres."));

        When(c => c.JerseyNumber is not null, () =>
            RuleFor(c => c.JerseyNumber!.Value)
                .InclusiveBetween(Player.MinJerseyNumber, Player.MaxJerseyNumber)
                .OverridePropertyName(nameof(PatchPlayerCommand.JerseyNumber))
                .WithMessage($"El número de camiseta debe estar entre {Player.MinJerseyNumber} y {Player.MaxJerseyNumber}."));

        When(c => c.Position is not null, () =>
            RuleFor(c => c.Position!.Value)
                .IsInEnum()
                .OverridePropertyName(nameof(PatchPlayerCommand.Position))
                .WithMessage("La posición debe ser Goalkeeper, Defender, Midfielder o Forward."));
    }
}
