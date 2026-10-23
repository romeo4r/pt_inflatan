using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.CreateTeam;

internal sealed class CreateTeamCommandHandler(
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateTeamCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTeamCommand command, CancellationToken cancellationToken)
    {
        // Uniqueness spans aggregates, so it is checked here; the unique index is the backstop
        // for two concurrent creates, and the Unit of Work turns that violation into a 409 too.
        if (await teamRepository.ExistsByNameAsync(command.Name!.Trim(), cancellationToken: cancellationToken))
        {
            return TeamErrors.NameAlreadyExists(command.Name.Trim());
        }

        var normalizedCode = Team.NormalizeCode(command.Code!);
        if (await teamRepository.ExistsByCodeAsync(normalizedCode, cancellationToken: cancellationToken))
        {
            return TeamErrors.CodeAlreadyExists(normalizedCode);
        }

        var teamResult = Team.Create(command.Name, command.Code, command.Coach);
        if (teamResult.IsFailure)
        {
            return teamResult.Error;
        }

        teamRepository.Add(teamResult.Value);

        var commit = await unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            return commit.Error;
        }

        return teamResult.Value.Id;
    }
}
