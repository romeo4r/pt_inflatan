using Mundialito.Domain.Common;

namespace Mundialito.Domain.Matches;

public static class MatchErrors
{
    public static readonly Error SameTeam = Error.Validation(
        "Match.SameTeam", "Un equipo no puede jugar contra sí mismo.");

    public static readonly Error MatchDayInvalid = Error.Validation(
        "Match.MatchDayInvalid", "La jornada debe ser un número mayor o igual a 1.");

    public static readonly Error VenueTooLong = Error.Validation(
        "Match.VenueTooLong", $"La sede no puede superar {Match.VenueMaxLength} caracteres.");

    public static readonly Error NegativeScore = Error.Validation(
        "Match.NegativeScore", "El marcador no puede ser negativo.");

    public static readonly Error MinuteOutOfRange = Error.Validation(
        "Match.MinuteOutOfRange", $"El minuto del gol debe estar entre {Goal.MinMinute} y {Goal.MaxMinute}.");

    public static readonly Error ScorerTeamNotInMatch = Error.Validation(
        "Match.ScorerTeamNotInMatch", "Cada gol debe pertenecer a uno de los dos equipos del partido.");

    public static readonly Error GoalsDoNotMatchScore = Error.Validation(
        "Match.GoalsDoNotMatchScore",
        "La cantidad de goles registrados por equipo no coincide con el marcador.");

    public static readonly Error AlreadyFinished = Error.Conflict(
        "Match.AlreadyFinished", "El partido ya tiene un resultado registrado.");

    public static readonly Error Cancelled = Error.Conflict(
        "Match.Cancelled", "El partido está cancelado y no admite cambios.");

    public static readonly Error NotScheduled = Error.Conflict(
        "Match.NotScheduled", "Solo se puede modificar un partido que está programado.");

    public static Error NotFound(Guid matchId) => Error.NotFound(
        "Match.NotFound", $"No existe un partido con el identificador {matchId}.");
}
