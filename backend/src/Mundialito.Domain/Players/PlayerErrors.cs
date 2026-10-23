using Mundialito.Domain.Common;

namespace Mundialito.Domain.Players;

public static class PlayerErrors
{
    public static readonly Error FirstNameRequired = Error.Validation(
        "Player.FirstNameRequired", "El nombre del jugador es obligatorio.");

    public static readonly Error LastNameRequired = Error.Validation(
        "Player.LastNameRequired", "El apellido del jugador es obligatorio.");

    public static readonly Error NameTooLong = Error.Validation(
        "Player.NameTooLong",
        $"El nombre y el apellido no pueden superar {Player.NameMaxLength} caracteres cada uno.");

    public static readonly Error JerseyNumberOutOfRange = Error.Validation(
        "Player.JerseyNumberOutOfRange",
        $"El número de camiseta debe estar entre {Player.MinJerseyNumber} y {Player.MaxJerseyNumber}.");

    public static readonly Error PositionInvalid = Error.Validation(
        "Player.PositionInvalid", "La posición del jugador no es válida.");

    public static readonly Error BirthDateInFuture = Error.Validation(
        "Player.BirthDateInFuture", "La fecha de nacimiento no puede ser futura.");

    public static Error NotFound(Guid playerId) => Error.NotFound(
        "Player.NotFound", $"No existe un jugador con el identificador {playerId}.");

    public static Error JerseyNumberTaken(int number) => Error.Conflict(
        "Player.JerseyNumberTaken", $"El número {number} ya está asignado a otro jugador del equipo.");

    public static readonly Error HasGoals = Error.Conflict(
        "Player.HasGoals", "No se puede eliminar un jugador que tiene goles registrados.");

    public static Error NotInTeam(Guid playerId, Guid teamId) => Error.Validation(
        "Player.NotInTeam", $"El jugador {playerId} pertenece al equipo {teamId}, que no participa en este partido.");
}
