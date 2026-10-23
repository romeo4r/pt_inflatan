using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Commands.RegisterMatchResult;

/// <summary>One goal as reported by the client. The team is derived from the player's roster.</summary>
public sealed record GoalInput(Guid PlayerId, int? Minute);

/// <summary>
/// Closes a scheduled match with its final score and the goals that produced it. Raises
/// MatchResultRegisteredDomainEvent through the aggregate. Own goals are out of scope: every goal
/// is credited to the scorer's own team.
/// </summary>
public sealed record RegisterMatchResultCommand(
    Guid MatchId,
    int HomeScore,
    int AwayScore,
    IReadOnlyList<GoalInput> Goals,
    byte[]? ExpectedRowVersion = null) : ICommand;
