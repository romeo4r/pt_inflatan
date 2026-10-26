using System.Diagnostics;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Behaviors;

/// <summary>
/// Opens one span per command/query so a trace shows which use case ran, how long it took and,
/// on failure, which error code it produced. Only type names and codes are tagged, never payloads.
/// The source is picked up by OpenTelemetry in the host; without a listener this is a no-op.
/// </summary>
internal sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public const string SourceName = "Mundialito.Application";

    public static readonly ActivitySource Source = new(SourceName);

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var kind = request is IBaseCommand ? "command" : "query";

        using var activity = Source.StartActivity($"{kind} {typeof(TRequest).Name}", ActivityKind.Internal);
        activity?.SetTag("mundialito.request.type", typeof(TRequest).Name);
        activity?.SetTag("mundialito.request.kind", kind);

        var response = await next();

        if (activity is null)
        {
            return response;
        }

        if (response.IsSuccess)
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            // Business failures are expected outcomes: tagged for filtering, not marked as span errors.
            activity.SetTag("mundialito.result", "failure");
            activity.SetTag("mundialito.error.code", response.Error.Code);
            activity.SetTag("mundialito.error.type", response.Error.Type.ToString());
        }

        return response;
    }
}
