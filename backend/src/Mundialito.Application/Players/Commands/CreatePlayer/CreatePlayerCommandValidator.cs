using FluentValidation;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Commands.CreatePlayer;

internal sealed class CreatePlayerCommandValidator : AbstractValidator<CreatePlayerCommand>
{
    public CreatePlayerCommandValidator()
    {
        RuleFor(c => c.TeamId)
            .NotEmpty().WithMessage("El identificador del equipo es obligatorio.");

        RuleFor(c => c.FirstName)
            .NotEmpty().WithMessage("El nombre del jugador es obligatorio.")
            .MaximumLength(Player.NameMaxLength).WithMessage($"El nombre no puede superar {Player.NameMaxLength} caracteres.");

        RuleFor(c => c.LastName)
            .NotEmpty().WithMessage("El apellido del jugador es obligatorio.")
            .MaximumLength(Player.NameMaxLength).WithMessage($"El apellido no puede superar {Player.NameMaxLength} caracteres.");

        RuleFor(c => c.JerseyNumber)
            .InclusiveBetween(Player.MinJerseyNumber, Player.MaxJerseyNumber)
            .WithMessage($"El número de camiseta debe estar entre {Player.MinJerseyNumber} y {Player.MaxJerseyNumber}.");

        RuleFor(c => c.Position)
            .IsInEnum().WithMessage("La posición debe ser Goalkeeper, Defender, Midfielder o Forward.");
    }
}
