using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Common;
using Mundialito.UnitTests.Application.Messaging;

namespace Mundialito.UnitTests.Application.Behaviors;

public sealed class TracingBehaviorTests : IDisposable
{
    private readonly List<Activity> _activities = [];
    private readonly ActivityListener _listener;

    public TracingBehaviorTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Mundialito.Application",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task Handle_Command_ShouldOpenSpanNamedAfterRequestAndMarkOk()
    {
        var sender = BuildSender(Result.Success());

        await sender.Send(new ProbeCommand());

        var span = _activities.ShouldHaveSingleItem();
        span.DisplayName.ShouldBe("command ProbeCommand");
        span.GetTagItem("mundialito.request.kind").ShouldBe("command");
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem("mundialito.error.code").ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Query_ShouldTagKindAsQuery()
    {
        var sender = BuildSender(Result.Success(1));

        await sender.Send(new ProbeQuery());

        _activities.ShouldHaveSingleItem().DisplayName.ShouldBe("query ProbeQuery");
    }

    [Fact]
    public async Task Handle_Failure_ShouldTagErrorCodeWithoutMarkingSpanAsError()
    {
        var sender = BuildSender(Result.Failure(Error.Conflict("Team.CodeAlreadyExists", "dup")));

        var result = await sender.Send(new ProbeCommand());

        result.IsFailure.ShouldBeTrue();
        var span = _activities.ShouldHaveSingleItem();
        span.GetTagItem("mundialito.result").ShouldBe("failure");
        span.GetTagItem("mundialito.error.code").ShouldBe("Team.CodeAlreadyExists");
        span.GetTagItem("mundialito.error.type").ShouldBe("Conflict");
        span.Status.ShouldNotBe(ActivityStatusCode.Error);
    }

    private static ISender BuildSender(Result commandResult)
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(ISender), DispatcherAccessor.DispatcherType);
        services.AddScoped(typeof(IPipelineBehavior<,>), DispatcherAccessor.TracingBehaviorType);
        services.AddScoped<IRequestHandler<ProbeCommand, Result>>(_ => new ProbeCommandHandler(commandResult));
        services.AddScoped<IRequestHandler<ProbeQuery, Result<int>>, ProbeQueryHandler>();

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    public sealed record ProbeCommand : ICommand;

    public sealed record ProbeQuery : IQuery<int>;

    private sealed class ProbeCommandHandler(Result result) : ICommandHandler<ProbeCommand>
    {
        public Task<Result> Handle(ProbeCommand request, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ProbeQueryHandler : IQueryHandler<ProbeQuery, int>
    {
        public Task<Result<int>> Handle(ProbeQuery request, CancellationToken cancellationToken) => Task.FromResult(Result.Success(1));
    }
}
