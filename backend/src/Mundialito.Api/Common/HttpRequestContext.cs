using Mundialito.Api.Middleware;
using Mundialito.Application.Abstractions.Events;

namespace Mundialito.Api.Common;

/// <summary>
/// Exposes the current request's correlation and trace ids to the application layer without
/// leaking HttpContext into it. Outside a request (seed, background work) both are null.
/// </summary>
public sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? CorrelationId => accessor.HttpContext?.GetCorrelationId();

    public string? TraceId => accessor.HttpContext?.GetTraceId();
}
