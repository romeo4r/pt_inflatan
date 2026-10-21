using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Common;

namespace Mundialito.UnitTests.Application.Messaging;

public class DispatcherTests
{
    [Fact]
    public async Task Send_ShouldRouteCommandToItsHandler()
    {
        var provider = BuildProvider(services =>
            services.AddScoped<IRequestHandler<PingCommand, Result<string>>, PingHandler>());
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new PingCommand("hola"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("pong: hola");
    }

    [Fact]
    public async Task Send_ShouldRouteQueryToItsHandler()
    {
        var provider = BuildProvider(services =>
            services.AddScoped<IRequestHandler<CountQuery, Result<int>>, CountHandler>());
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new CountQuery(3));

        result.Value.ShouldBe(6);
    }

    [Fact]
    public async Task Send_WithoutHandler_ShouldThrowInvalidOperation()
    {
        var provider = BuildProvider(_ => { });
        var sender = provider.GetRequiredService<ISender>();

        await Should.ThrowAsync<InvalidOperationException>(() => sender.Send(new PingCommand("x")));
    }

    [Fact]
    public async Task Send_ShouldRunBehaviorsInRegistrationOrderAroundHandler()
    {
        var log = new List<string>();
        var provider = BuildProvider(services =>
        {
            services.AddSingleton(log);
            services.AddScoped<IRequestHandler<PingCommand, Result<string>>, PingHandler>();
            services.AddScoped<IPipelineBehavior<PingCommand, Result<string>>>(
                sp => new RecordingBehavior("first", sp.GetRequiredService<List<string>>()));
            services.AddScoped<IPipelineBehavior<PingCommand, Result<string>>>(
                sp => new RecordingBehavior("second", sp.GetRequiredService<List<string>>()));
        });
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PingCommand("x"));

        log.ShouldBe(["first:before", "second:before", "handler", "second:after", "first:after"]);
    }

    [Fact]
    public async Task Send_ShouldAllowBehaviorToShortCircuit()
    {
        var error = Error.Conflict("Test.Blocked", "bloqueado");
        var provider = BuildProvider(services =>
        {
            services.AddScoped<IRequestHandler<PingCommand, Result<string>>, PingHandler>();
            services.AddScoped<IPipelineBehavior<PingCommand, Result<string>>>(_ => new ShortCircuitBehavior(error));
        });
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new PingCommand("x"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(ISender), DispatcherAccessor.DispatcherType);
        configure(services);
        return services.BuildServiceProvider();
    }

    public sealed record PingCommand(string Text) : ICommand<string>;

    public sealed record CountQuery(int Times) : IQuery<int>;

    private sealed class PingHandler(List<string>? log = null) : ICommandHandler<PingCommand, string>
    {
        public Task<Result<string>> Handle(PingCommand request, CancellationToken cancellationToken)
        {
            log?.Add("handler");
            return Task.FromResult(Result.Success($"pong: {request.Text}"));
        }
    }

    private sealed class CountHandler : IQueryHandler<CountQuery, int>
    {
        public Task<Result<int>> Handle(CountQuery request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(request.Times * 2));
    }

    private sealed class RecordingBehavior(string name, List<string> log)
        : IPipelineBehavior<PingCommand, Result<string>>
    {
        public async Task<Result<string>> Handle(
            PingCommand request,
            RequestHandlerDelegate<Result<string>> next,
            CancellationToken cancellationToken)
        {
            log.Add($"{name}:before");
            var response = await next();
            log.Add($"{name}:after");
            return response;
        }
    }

    private sealed class ShortCircuitBehavior(Error error) : IPipelineBehavior<PingCommand, Result<string>>
    {
        public Task<Result<string>> Handle(
            PingCommand request,
            RequestHandlerDelegate<Result<string>> next,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<string>(error));
    }
}
