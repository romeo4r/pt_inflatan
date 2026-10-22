using Mundialito.Domain.Common;

namespace Mundialito.Api.Common;

/// <summary>
/// Translates the Result Pattern into HTTP. The mapping is driven by <see cref="ErrorType"/>, so
/// endpoints never inspect error codes: Validation and generic failures become 400, NotFound 404
/// and Conflict 409. Bodies follow RFC 9457 (ProblemDetails) with <c>code</c> as an extension and
/// the Spanish message in <c>detail</c>; validation errors add the per-field <c>errors</c> map.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error.ToProblem();

    public static IResult ToHttpResult<TValue>(this Result<TValue> result, Func<TValue, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();

    /// <summary>Shortcut for the common "200 OK with the value as body" case.</summary>
    public static IResult ToOk<TValue>(this Result<TValue> result) =>
        result.ToHttpResult(value => Results.Ok(value));

    public static IResult ToProblem(this Error error)
    {
        if (error is ValidationError validationError)
        {
            return Results.ValidationProblem(
                validationError.Errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
                detail: validationError.Message,
                title: "Validación fallida",
                extensions: CodeExtension(validationError.Code));
        }

        var (statusCode, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            _ => (StatusCodes.Status400BadRequest, "La operación no pudo completarse"),
        };

        return Results.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: title,
            extensions: CodeExtension(error.Code));
    }

    private static Dictionary<string, object?> CodeExtension(string code) =>
        new(StringComparer.Ordinal) { ["code"] = code };
}
