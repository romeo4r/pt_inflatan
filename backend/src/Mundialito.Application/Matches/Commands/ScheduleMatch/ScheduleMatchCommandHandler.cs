using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Matches.Commands.ScheduleMatch;

internal sealed class ScheduleMatchCommandHandler(
    IMatchRepository matchRepository,
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<ScheduleMatchCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ScheduleMatchCommand command, CancellationToken cancellationToken)
    {
        if (await teamRepository.GetByIdAsync(command.HomeTeamId, cancellationToken) is null)
        {
            return TeamErrors.NotFound(command.HomeTeamId);
        }

        if (await teamRepository.GetByIdAsync(command.AwayTeamId, cancellationToken) is null)
        {
            return TeamErrors.NotFound(command.AwayTeamId);
        }

        var matchResult = Match.Schedule(
            command.HomeTeamId,
            command.AwayTeamId,
            command.MatchDay,
            command.ScheduledAtUtc,
            command.Venue);

        if (matchResult.IsFailure)
        {
            return matchResult.Error;
        }

        matchRepository.Add(matchResult.Value);

        var commit = await unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            return commit.Error;
        }

        return matchResult.Value.Id;
    }
}
