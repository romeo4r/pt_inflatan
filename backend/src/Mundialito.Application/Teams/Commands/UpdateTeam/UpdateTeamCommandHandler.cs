using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.UpdateTeam;

internal sealed class UpdateTeamCommandHandler(
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateTeamCommand>
{
    public async Task<Result> Handle(UpdateTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(command.Id, cancellationToken);
        if (team is null)
        {
            return Result.Failure(TeamErrors.NotFound(command.Id));
        }

        // Stale client: it read an older version than the one stored now.
        if (command.ExpectedRowVersion is not null && !team.HasVersion(command.ExpectedRowVersion))
        {
            return Result.Failure(PersistenceErrors.ConcurrencyConflict);
        }

        if (await teamRepository.ExistsByNameAsync(command.Name!.Trim(), excludeId: team.Id, cancellationToken))
        {
            return Result.Failure(TeamErrors.NameAlreadyExists(command.Name.Trim()));
        }

        var normalizedCode = Team.NormalizeCode(command.Code!);
        if (await teamRepository.ExistsByCodeAsync(normalizedCode, excludeId: team.Id, cancellationToken))
        {
            return Result.Failure(TeamErrors.CodeAlreadyExists(normalizedCode));
        }

        var updateResult = team.Update(command.Name, command.Code, command.Coach);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        if (command.IsActive)
        {
            team.Activate();
        }
        else
        {
            team.Deactivate();
        }

        // The database re-checks the version at write time, which covers the race between read and commit.
        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }
}
