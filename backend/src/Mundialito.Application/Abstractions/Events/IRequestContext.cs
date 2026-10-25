namespace Mundialito.Application.Abstractions.Events;

/// <summary>
/// Identifiers of the request being processed, so persisted events can be linked back to the
/// HTTP call and its log lines. Implemented by the host (API); null outside a request.
/// </summary>
public interface IRequestContext
{
    string? CorrelationId { get; }

    string? TraceId { get; }
}
