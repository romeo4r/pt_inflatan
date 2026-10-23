using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Common;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Players.Commands.CreatePlayer;

internal sealed class CreatePlayerCommandHandler(
    IPlayerRepository playerRepository,
    ITeamRepository teamRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CreatePlayerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreatePlayerCommand command, CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(command.TeamId, cancellationToken);
        if (team is null)
        {
            return TeamErrors.NotFound(command.TeamId);
        }

        if (await playerRepository.ExistsByJerseyNumberAsync(team.Id, command.JerseyNumber, cancellationToken: cancellationToken))
        {
            return PlayerErrors.JerseyNumberTaken(command.JerseyNumber);
        }

        var playerResult = Player.Create(
            team.Id,
            command.FirstName,
            command.LastName,
            command.JerseyNumber,
            command.Position,
            command.BirthDate,
            clock.Today);

        if (playerResult.IsFailure)
        {
            return playerResult.Error;
        }

        playerRepository.Add(playerResult.Value);

        var commit = await unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            return commit.Error;
        }

        return playerResult.Value.Id;
    }
}
