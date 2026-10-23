using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Commands.ScheduleMatch;

/// <summary>Creates a fixture between two existing teams and returns the match id.</summary>
public sealed record ScheduleMatchCommand(
    Guid HomeTeamId,
    Guid AwayTeamId,
    int MatchDay,
    DateTime ScheduledAtUtc,
    string? Venue) : ICommand<Guid>;
