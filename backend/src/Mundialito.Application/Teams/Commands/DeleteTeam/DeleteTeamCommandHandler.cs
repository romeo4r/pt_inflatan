using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.DeleteTeam;

internal sealed class DeleteTeamCommandHandler(
    ITeamRepository teamRepository,
    IMatchRepository matchRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteTeamCommand>
{
    public async Task<Result> Handle(DeleteTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(command.Id, cancellationToken);
        if (team is null)
        {
            return Result.Success();
        }

        if (await matchRepository.AnyInvolvingTeamAsync(team.Id, cancellationToken))
        {
            return Result.Failure(TeamErrors.HasMatches);
        }

        teamRepository.Remove(team);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
