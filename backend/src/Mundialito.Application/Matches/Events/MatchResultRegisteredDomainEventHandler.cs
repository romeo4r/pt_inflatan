using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Application.Events;
using Mundialito.Domain.Matches.Events;

namespace Mundialito.Application.Matches.Events;

/// <summary>
/// Reacts to a registered result: structured log line plus a persisted trace in the audit table,
/// both inside the transaction that stored the score and the goals.
/// </summary>
internal sealed partial class MatchResultRegisteredDomainEventHandler(
    IDomainEventLogRepository eventLog,
    IRequestContext requestContext,
    IClock clock,
    ILogger<MatchResultRegisteredDomainEventHandler> logger) : IDomainEventHandler<MatchResultRegisteredDomainEvent>
{
    public Task Handle(MatchResultRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        LogResultRegistered(
            logger,
            domainEvent.MatchId,
            domainEvent.HomeTeamId,
            domainEvent.HomeScore,
            domainEvent.AwayScore,
            domainEvent.AwayTeamId,
            domainEvent.EventId);

        eventLog.Add(DomainEventLogEntryFactory.Create(domainEvent, domainEvent.MatchId, requestContext, clock));

        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Result registered for match {MatchId}: {HomeTeamId} {HomeScore}-{AwayScore} {AwayTeamId}; event {DomainEventId}")]
    private static partial void LogResultRegistered(
        ILogger logger,
        Guid matchId,
        Guid homeTeamId,
        int homeScore,
        int awayScore,
        Guid awayTeamId,
        Guid domainEventId);
}
