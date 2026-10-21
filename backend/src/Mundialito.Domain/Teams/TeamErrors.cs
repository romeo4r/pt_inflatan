using Mundialito.Domain.Common;

namespace Mundialito.Domain.Teams;

public static class TeamErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "Team.NameRequired", "El nombre del equipo es obligatorio.");

    public static readonly Error NameTooLong = Error.Validation(
        "Team.NameTooLong", $"El nombre del equipo no puede superar {Team.NameMaxLength} caracteres.");

    public static readonly Error CodeRequired = Error.Validation(
        "Team.CodeRequired", "El código del equipo es obligatorio.");

    public static readonly Error CodeInvalid = Error.Validation(
        "Team.CodeInvalid",
        $"El código del equipo debe tener entre {Team.CodeMinLength} y {Team.CodeMaxLength} letras o números.");

    public static readonly Error CoachTooLong = Error.Validation(
        "Team.CoachTooLong", $"El nombre del entrenador no puede superar {Team.CoachMaxLength} caracteres.");

    public static readonly Error HasMatches = Error.Conflict(
        "Team.HasMatches", "No se puede eliminar un equipo que tiene partidos asociados.");

    public static Error NotFound(Guid teamId) => Error.NotFound(
        "Team.NotFound", $"No existe un equipo con el identificador {teamId}.");

    public static Error NameAlreadyExists(string name) => Error.Conflict(
        "Team.NameAlreadyExists", $"Ya existe un equipo con el nombre '{name}'.");

    public static Error CodeAlreadyExists(string code) => Error.Conflict(
        "Team.CodeAlreadyExists", $"Ya existe un equipo con el código '{code}'.");
}
