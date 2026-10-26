using System.Diagnostics;
using Serilog.Context;

namespace Mundialito.Api.Middleware;

/// <summary>
/// Gives every request a correlation id (taken from the X-Correlation-Id header when the caller
/// sends a valid one, generated otherwise) and a trace id (from the current Activity), echoes both
/// in the response headers and pushes them into the Serilog context so every log line written
/// while handling the request carries them.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const string TraceIdHeader = "X-Trace-Id";
    public const string CorrelationIdItemKey = "CorrelationId";
    public const string TraceIdItemKey = "TraceId";

    private const int MaxCorrelationIdLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request);
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Items[CorrelationIdItemKey] = correlationId;
        context.Items[TraceIdItemKey] = traceId;

        // Makes the trace searchable by correlation id in the tracing backend.
        Activity.Current?.SetTag("correlation.id", correlationId);
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.Response.Headers[TraceIdHeader] = traceId;

        using (LogContext.PushProperty(CorrelationIdItemKey, correlationId))
        using (LogContext.PushProperty(TraceIdItemKey, traceId))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Accepts the caller's id only when it is short and made of safe characters, so a malicious
    /// header cannot inject newlines or markup into logs.
    /// </summary>
    private static string ResolveCorrelationId(HttpRequest request)
    {
        var incoming = request.Headers[CorrelationIdHeader].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(incoming)
            && incoming.Length <= MaxCorrelationIdLength
            && incoming.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            return incoming;
        }

        return Guid.CreateVersion7().ToString();
    }
}

public static class CorrelationIdHttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string ?? context.TraceIdentifier;

    public static string GetTraceId(this HttpContext context) =>
        context.Items[CorrelationIdMiddleware.TraceIdItemKey] as string ?? context.TraceIdentifier;
}
