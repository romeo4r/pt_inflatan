using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Common;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Commands.PatchPlayer;

internal sealed class PatchPlayerCommandHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<PatchPlayerCommand>
{
    public async Task<Result> Handle(PatchPlayerCommand command, CancellationToken cancellationToken)
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

        // Merge: a null member means "keep the current value".
        var jerseyNumber = command.JerseyNumber ?? player.JerseyNumber;

        if (command.JerseyNumber is not null
            && await playerRepository.ExistsByJerseyNumberAsync(player.TeamId, jerseyNumber, excludeId: player.Id, cancellationToken))
        {
            return Result.Failure(PlayerErrors.JerseyNumberTaken(jerseyNumber));
        }

        var updateResult = player.Update(
            command.FirstName ?? player.FirstName,
            command.LastName ?? player.LastName,
            jerseyNumber,
            command.Position ?? player.Position,
            command.BirthDate ?? player.BirthDate,
            clock.Today);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        switch (command.IsActive)
        {
            case true:
                player.Activate();
                break;
            case false:
                player.Deactivate();
                break;
        }

        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }
}
