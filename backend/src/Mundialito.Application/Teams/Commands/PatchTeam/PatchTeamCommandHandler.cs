using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.PatchTeam;

internal sealed class PatchTeamCommandHandler(
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<PatchTeamCommand>
{
    public async Task<Result> Handle(PatchTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(command.Id, cancellationToken);
        if (team is null)
        {
            return Result.Failure(TeamErrors.NotFound(command.Id));
        }

        // Merge: a null member means "keep the current value".
        var name = command.Name ?? team.Name;
        var code = command.Code ?? team.Code;
        var coach = command.Coach ?? team.Coach;

        if (command.Name is not null
            && await teamRepository.ExistsByNameAsync(name.Trim(), excludeId: team.Id, cancellationToken))
        {
            return Result.Failure(TeamErrors.NameAlreadyExists(name.Trim()));
        }

        var normalizedCode = Team.NormalizeCode(code);
        if (command.Code is not null
            && await teamRepository.ExistsByCodeAsync(normalizedCode, excludeId: team.Id, cancellationToken))
        {
            return Result.Failure(TeamErrors.CodeAlreadyExists(normalizedCode));
        }

        var updateResult = team.Update(name, code, coach);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        switch (command.IsActive)
        {
            case true:
                team.Activate();
                break;
            case false:
                team.Deactivate();
                break;
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
