namespace Mundialito.Domain.Common;

/// <summary>
/// Error that carries field-level details. The API serializes <see cref="Errors"/> as
/// <c>{ "fieldName": ["message", ...] }</c> so clients can highlight the offending inputs.
/// </summary>
public sealed record ValidationError : Error
{
    public const string DefaultCode = "Validation.Failed";
    public const string DefaultMessage = "Uno o más campos son inválidos.";

    public ValidationError(IReadOnlyDictionary<string, string[]> errors)
        : base(DefaultCode, DefaultMessage, ErrorType.Validation)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
