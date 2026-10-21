using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Messaging;

/// <summary>
/// Entry point used by the API to execute commands and queries. The API never talks to a handler
/// directly, so cross-cutting behaviors are guaranteed to run for every request.
/// </summary>
public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : Result;
}
