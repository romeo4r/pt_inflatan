using System.Security.Cryptography;
using System.Text;
using Mundialito.Api.Common;
using Mundialito.Application.Abstractions.Idempotency;
using Mundialito.Domain.Common;

namespace Mundialito.Api.Idempotency;

/// <summary>
/// Implements Idempotency-Key for endpoints marked with <see cref="IdempotentEndpointMetadata"/>.
/// First call with a key: the key is reserved, the handler runs, the response is captured and
/// stored. Any later call with the same key and the same payload gets the stored response back,
/// byte for byte, without touching the handler, so a retried POST can never create a duplicate.
/// Same key with a different payload or while the first call is still running answers 409.
/// </summary>
public sealed partial class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";

    private const int MaxKeyLength = 128;
    private const long MaxStoredBodyBytes = 1024 * 1024;

    private static readonly Error KeyRequired = Error.Validation(
        "Idempotency.KeyRequired",
        $"La cabecera {HeaderName} es obligatoria para esta operación.");

    private static readonly Error KeyInvalid = Error.Validation(
        "Idempotency.KeyInvalid",
        $"La cabecera {HeaderName} debe tener entre 1 y {MaxKeyLength} caracteres (letras, números, '-', '_', '.', ':').");

    private static readonly Error PayloadMismatch = Error.Conflict(
        "Idempotency.PayloadMismatch",
        "La misma Idempotency-Key ya fue usada con un contenido diferente.");

    private static readonly Error RequestInProgress = Error.Conflict(
        "Idempotency.RequestInProgress",
        "Una petición con la misma Idempotency-Key todavía se está procesando.");

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotentEndpointMetadata>() is null)
        {
            await next(context);
            return;
        }

        var header = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
        {
            await KeyRequired.ToProblem().ExecuteAsync(context);
            return;
        }

        if (!IsValidKey(header))
        {
            await KeyInvalid.ToProblem().ExecuteAsync(context);
            return;
        }

        var key = header.Trim();
        var requestHash = await ComputeRequestHashAsync(context.Request, context.RequestAborted);

        var reservation = await store.TryReserveAsync(
            key, requestHash, context.Request.Method, context.Request.Path, context.RequestAborted);

        switch (reservation.Outcome)
        {
            case ReservationOutcome.Completed:
                LogReplayed(logger, key);
                await ReplayAsync(context, reservation.Response!);
                return;

            case ReservationOutcome.PayloadMismatch:
                LogPayloadMismatch(logger, key);
                await PayloadMismatch.ToProblem().ExecuteAsync(context);
                return;

            case ReservationOutcome.InProgress:
                LogInProgress(logger, key);
                await RequestInProgress.ToProblem().ExecuteAsync(context);
                return;
        }

        await ExecuteAndCaptureAsync(context, store, key);
    }

    /// <summary>
    /// Runs the rest of the pipeline with the response body redirected to a buffer, stores the
    /// outcome (unless it is a server error) and finally streams the buffer to the client.
    /// </summary>
    private async Task ExecuteAndCaptureAsync(HttpContext context, IIdempotencyStore store, string key)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        catch
        {
            context.Response.Body = originalBody;
            await store.ReleaseAsync(key, CancellationToken.None);
            throw;
        }

        context.Response.Body = originalBody;
        buffer.Position = 0;

        var statusCode = context.Response.StatusCode;
        if (statusCode < StatusCodes.Status500InternalServerError && buffer.Length <= MaxStoredBodyBytes)
        {
            var body = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            await store.CompleteAsync(
                key,
                new IdempotentResponse(statusCode, context.Response.ContentType, body, context.Response.Headers.Location),
                CancellationToken.None);
            LogStored(logger, key, statusCode);
        }
        else
        {
            // Server errors are not remembered: the client should be able to retry with the same key.
            await store.ReleaseAsync(key, CancellationToken.None);
        }

        await buffer.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static async Task ReplayAsync(HttpContext context, IdempotentResponse stored)
    {
        context.Response.StatusCode = stored.StatusCode;
        context.Response.Headers[ReplayedHeaderName] = "true";

        if (stored.ContentType is not null)
        {
            context.Response.ContentType = stored.ContentType;
        }

        if (stored.Location is not null)
        {
            context.Response.Headers.Location = stored.Location;
        }

        await context.Response.WriteAsync(stored.Body, context.RequestAborted);
    }

    /// <summary>SHA-256 over method, path and body so the same key cannot be reused for a different request.</summary>
    private static async Task<string> ComputeRequestHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        using var sha256 = SHA256.Create();
        var prefix = Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path}\n");
        sha256.TransformBlock(prefix, 0, prefix.Length, null, 0);

        var bodyBuffer = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(bodyBuffer, cancellationToken)) > 0)
        {
            sha256.TransformBlock(bodyBuffer, 0, read, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        request.Body.Position = 0;

        return Convert.ToHexString(sha256.Hash!);
    }

    private static bool IsValidKey(string value)
    {
        var key = value.Trim();

        return key.Length is > 0 and <= MaxKeyLength
            && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Idempotent request {IdempotencyKey} replayed from store")]
    private static partial void LogReplayed(ILogger logger, string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Idempotent request {IdempotencyKey} stored with status {StatusCode}")]
    private static partial void LogStored(ILogger logger, string idempotencyKey, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idempotency key {IdempotencyKey} reused with a different payload")]
    private static partial void LogPayloadMismatch(ILogger logger, string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idempotency key {IdempotencyKey} is still being processed")]
    private static partial void LogInProgress(ILogger logger, string idempotencyKey);
}
