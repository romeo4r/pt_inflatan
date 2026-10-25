using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Matches.Events;
using Mundialito.Application.Teams.Events;
using Mundialito.Domain.Matches.Events;
using Mundialito.Domain.Teams.Events;
using Mundialito.UnitTests.Infrastructure;
using NSubstitute;

namespace Mundialito.UnitTests.Application.Events;

public class DomainEventHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDomainEventLogRepository _eventLog = Substitute.For<IDomainEventLogRepository>();
    private readonly IRequestContext _requestContext = Substitute.For<IRequestContext>();
    private readonly FixedClock _clock = new(Now);

    public DomainEventHandlerTests()
    {
        _requestContext.CorrelationId.Returns("corr-1");
        _requestContext.TraceId.Returns("trace-1");
    }

    [Fact]
    public async Task TeamCreated_ShouldPersistEntryWithJsonPayloadAndRequestIds()
    {
        var domainEvent = new TeamCreatedDomainEvent(Guid.CreateVersion7(), "Finanzas", "FIN");
        DomainEventLogEntry? captured = null;
        _eventLog.When(r => r.Add(Arg.Any<DomainEventLogEntry>())).Do(c => captured = c.Arg<DomainEventLogEntry>());
        var handler = new TeamCreatedDomainEventHandler(_eventLog, _requestContext, _clock, NullLogger<TeamCreatedDomainEventHandler>.Instance);

        await handler.Handle(domainEvent, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.Id.ShouldBe(domainEvent.EventId);
        captured.EventType.ShouldBe(nameof(TeamCreatedDomainEvent));
        captured.AggregateId.ShouldBe(domainEvent.TeamId);
        captured.CorrelationId.ShouldBe("corr-1");
        captured.TraceId.ShouldBe("trace-1");
        captured.OccurredAtUtc.ShouldBe(domainEvent.OccurredAtUtc);
        captured.RecordedAtUtc.ShouldBe(Now);

        using var payload = JsonDocument.Parse(captured.Payload);
        payload.RootElement.GetProperty("teamId").GetGuid().ShouldBe(domainEvent.TeamId);
        payload.RootElement.GetProperty("code").GetString().ShouldBe("FIN");
        payload.RootElement.GetProperty("name").GetString().ShouldBe("Finanzas");
    }

    [Fact]
    public async Task MatchResultRegistered_ShouldPersistEntryKeyedByMatch()
    {
        var domainEvent = new MatchResultRegisteredDomainEvent(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 2, 1);
        DomainEventLogEntry? captured = null;
        _eventLog.When(r => r.Add(Arg.Any<DomainEventLogEntry>())).Do(c => captured = c.Arg<DomainEventLogEntry>());
        var handler = new MatchResultRegisteredDomainEventHandler(_eventLog, _requestContext, _clock, NullLogger<MatchResultRegisteredDomainEventHandler>.Instance);

        await handler.Handle(domainEvent, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.EventType.ShouldBe(nameof(MatchResultRegisteredDomainEvent));
        captured.AggregateId.ShouldBe(domainEvent.MatchId);
        using var payload = JsonDocument.Parse(captured.Payload);
        payload.RootElement.GetProperty("homeScore").GetInt32().ShouldBe(2);
        payload.RootElement.GetProperty("awayScore").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Handlers_ShouldWriteStructuredInformationLog()
    {
        // The handler type is internal, so ILogger<T> cannot be proxied directly; wrap a plain ILogger.
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(LogLevel.Information).Returns(true);
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);
        var handler = new TeamCreatedDomainEventHandler(
            _eventLog, _requestContext, _clock, new Logger<TeamCreatedDomainEventHandler>(loggerFactory));
        var domainEvent = new TeamCreatedDomainEvent(Guid.CreateVersion7(), "Finanzas", "FIN");

        await handler.Handle(domainEvent, CancellationToken.None);

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("FIN") && state.ToString()!.Contains(domainEvent.TeamId.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handlers_OutsideARequest_ShouldStoreNullIdentifiers()
    {
        // NSubstitute auto-returns "" for strings; make the "no request" case explicit.
        var noRequest = Substitute.For<IRequestContext>();
        noRequest.CorrelationId.Returns((string?)null);
        noRequest.TraceId.Returns((string?)null);
        DomainEventLogEntry? captured = null;
        _eventLog.When(r => r.Add(Arg.Any<DomainEventLogEntry>())).Do(c => captured = c.Arg<DomainEventLogEntry>());
        var handler = new TeamCreatedDomainEventHandler(_eventLog, noRequest, _clock, NullLogger<TeamCreatedDomainEventHandler>.Instance);

        await handler.Handle(new TeamCreatedDomainEvent(Guid.CreateVersion7(), "Finanzas", "FIN"), CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.CorrelationId.ShouldBeNull();
        captured.TraceId.ShouldBeNull();
    }
}
