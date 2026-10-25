using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Application.Events;
using Mundialito.Domain.Teams.Events;

namespace Mundialito.Application.Teams.Events;

/// <summary>
/// Reacts to a new team: writes a structured log line and persists the event in the audit table.
/// Runs inside the creating transaction, so the trace exists if and only if the team does.
/// </summary>
internal sealed partial class TeamCreatedDomainEventHandler(
    IDomainEventLogRepository eventLog,
    IRequestContext requestContext,
    IClock clock,
    ILogger<TeamCreatedDomainEventHandler> logger) : IDomainEventHandler<TeamCreatedDomainEvent>
{
    public Task Handle(TeamCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        LogTeamCreated(logger, domainEvent.TeamId, domainEvent.Code, domainEvent.Name, domainEvent.EventId);

        eventLog.Add(DomainEventLogEntryFactory.Create(domainEvent, domainEvent.TeamId, requestContext, clock));

        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Team {TeamId} ({TeamCode}, {TeamName}) was created; event {DomainEventId}")]
    private static partial void LogTeamCreated(ILogger logger, Guid teamId, string teamCode, string teamName, Guid domainEventId);
}
