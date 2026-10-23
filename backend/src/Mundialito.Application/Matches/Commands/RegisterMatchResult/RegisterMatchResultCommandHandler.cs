using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Matches.Commands.RegisterMatchResult;

internal sealed class RegisterMatchResultCommandHandler(
    IMatchRepository matchRepository,
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<RegisterMatchResultCommand>
{
    public async Task<Result> Handle(RegisterMatchResultCommand command, CancellationToken cancellationToken)
    {
        var match = await matchRepository.GetByIdAsync(command.MatchId, cancellationToken);
        if (match is null)
        {
            return Result.Failure(MatchErrors.NotFound(command.MatchId));
        }

        if (command.ExpectedRowVersion is not null && !match.HasVersion(command.ExpectedRowVersion))
        {
            return Result.Failure(PersistenceErrors.ConcurrencyConflict);
        }

        // Fail fast on state before loading players: a finished match never gets this far.
        if (match.Status == MatchStatus.Finished)
        {
            return Result.Failure(MatchErrors.AlreadyFinished);
        }

        if (match.Status == MatchStatus.Cancelled)
        {
            return Result.Failure(MatchErrors.Cancelled);
        }

        var goalsResult = await ResolveGoalsAsync(match, command.Goals, cancellationToken);
        if (goalsResult.IsFailure)
        {
            return Result.Failure(goalsResult.Error);
        }

        var registerResult = match.RegisterResult(command.HomeScore, command.AwayScore, goalsResult.Value, clock.UtcNow);
        if (registerResult.IsFailure)
        {
            return registerResult;
        }

        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }

    /// <summary>
    /// Turns player ids into domain goal entries, crediting each goal to the scorer's own team.
    /// Only the application layer can do this because it needs the player roster.
    /// </summary>
    private async Task<Result<IReadOnlyCollection<GoalEntry>>> ResolveGoalsAsync(
        Match match,
        IReadOnlyList<GoalInput> goals,
        CancellationToken cancellationToken)
    {
        if (goals.Count == 0)
        {
            return Array.Empty<GoalEntry>();
        }

        var players = await playerRepository.GetByIdsAsync(goals.Select(g => g.PlayerId), cancellationToken);
        var playersById = players.ToDictionary(p => p.Id);

        var entries = new List<GoalEntry>(goals.Count);
        foreach (var goal in goals)
        {
            if (!playersById.TryGetValue(goal.PlayerId, out var player))
            {
                return PlayerErrors.NotFound(goal.PlayerId);
            }

            if (!match.Involves(player.TeamId))
            {
                return PlayerErrors.NotInTeam(player.Id, player.TeamId);
            }

            entries.Add(new GoalEntry(player.Id, player.TeamId, goal.Minute));
        }

        return entries;
    }
}
