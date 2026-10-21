using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Messaging;

/// <summary>
/// Minimal in-process mediator. Resolves the handler for the runtime type of the request,
/// wraps it with the registered <see cref="IPipelineBehavior{TRequest,TResponse}"/> chain and executes it.
/// The generic wrapper per request type is cached so reflection happens once per type.
/// </summary>
internal sealed class Dispatcher(IServiceProvider serviceProvider) : ISender
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : Result
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            static requestType =>
            {
                var responseType = requestType
                    .GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                    .GetGenericArguments()[0];

                var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
                return Activator.CreateInstance(wrapperType)!;
            });

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    private abstract class RequestHandlerWrapper<TResponse>
        where TResponse : Result
    {
        public abstract Task<TResponse> Handle(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }

    private sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : Result
    {
        public override Task<TResponse> Handle(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;
            var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typedRequest, cancellationToken);

            // Wrap from the innermost outwards so the first registered behavior ends up outermost.
            var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse();
            foreach (var behavior in behaviors)
            {
                var next = pipeline;
                pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
            }

            return pipeline();
        }
    }
}
