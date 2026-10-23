using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Commands.RescheduleMatch;

/// <summary>
/// Full replacement (PUT) of the fixture data of a scheduled match. The two teams are immutable:
/// changing the pairing is a different fixture, so it is modelled as delete + create.
/// </summary>
public sealed record RescheduleMatchCommand(
    Guid Id,
    int MatchDay,
    DateTime ScheduledAtUtc,
    string? Venue,
    byte[]? ExpectedRowVersion = null) : ICommand;
