using Mundialito.Application.Matches.Commands.RegisterMatchResult;

namespace Mundialito.Api.Contracts.Matches;

public sealed record GoalRequest(Guid PlayerId, int? Minute);

/// <summary>Body of POST /api/matches/{id}/result.</summary>
public sealed record RegisterMatchResultRequest(int HomeScore, int AwayScore, IReadOnlyList<GoalRequest>? Goals)
{
    public RegisterMatchResultCommand ToCommand(Guid matchId, byte[]? expectedRowVersion) => new(
        matchId,
        HomeScore,
        AwayScore,
        (Goals ?? []).Select(g => new GoalInput(g.PlayerId, g.Minute)).ToList(),
        expectedRowVersion);
}
