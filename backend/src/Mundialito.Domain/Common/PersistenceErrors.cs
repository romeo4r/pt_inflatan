namespace Mundialito.Domain.Common;

/// <summary>
/// Errors that originate in the storage layer but are business-meaningful to the caller.
/// Both map to 409 Conflict: the request was well-formed but clashes with the current state.
/// </summary>
public static class PersistenceErrors
{
    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "Concurrency.Conflict",
        "El registro fue modificado por otro usuario. Recargue los datos e intente de nuevo.");

    public static readonly Error UniqueViolation = Error.Conflict(
        "Database.UniqueViolation",
        "El registro entra en conflicto con otro existente (valor duplicado).");
}
