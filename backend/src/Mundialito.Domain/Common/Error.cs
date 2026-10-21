namespace Mundialito.Domain.Common;

/// <summary>
/// Classifies an error so the API layer can map it to an HTTP status code
/// without inspecting error codes or messages.
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
}

/// <summary>
/// Immutable description of a failure. <see cref="Code"/> is a stable, machine-readable
/// identifier in English; <see cref="Message"/> is the user-facing text in Spanish.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static readonly Error NullValue = new(
        "General.NullValue",
        "Se esperaba un valor pero se recibió nulo.",
        ErrorType.Failure);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}
