using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Common;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Commands.UpdatePlayer;

internal sealed class UpdatePlayerCommandHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<UpdatePlayerCommand>
{
    public async Task<Result> Handle(UpdatePlayerCommand command, CancellationToken cancellationToken)
    {
        var player = await playerRepository.GetByIdAsync(command.Id, cancellationToken);
        if (player is null)
        {
            return Result.Failure(PlayerErrors.NotFound(command.Id));
        }

        if (command.ExpectedRowVersion is not null && !player.HasVersion(command.ExpectedRowVersion))
        {
            return Result.Failure(PersistenceErrors.ConcurrencyConflict);
        }

        if (await playerRepository.ExistsByJerseyNumberAsync(player.TeamId, command.JerseyNumber, excludeId: player.Id, cancellationToken))
        {
            return Result.Failure(PlayerErrors.JerseyNumberTaken(command.JerseyNumber));
        }

        var updateResult = player.Update(
            command.FirstName,
            command.LastName,
            command.JerseyNumber,
            command.Position,
            command.BirthDate,
            clock.Today);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        if (command.IsActive)
        {
            player.Activate();
        }
        else
        {
            player.Deactivate();
        }

        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }
}
