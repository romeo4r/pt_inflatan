using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Players;

namespace Mundialito.Application.Players.Commands.DeletePlayer;

internal sealed class DeletePlayerCommandHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeletePlayerCommand>
{
    public async Task<Result> Handle(DeletePlayerCommand command, CancellationToken cancellationToken)
    {
        var player = await playerRepository.GetByIdAsync(command.Id, cancellationToken);
        if (player is null)
        {
            return Result.Success();
        }

        if (await playerRepository.HasGoalsAsync(player.Id, cancellationToken))
        {
            return Result.Failure(PlayerErrors.HasGoals);
        }

        playerRepository.Remove(player);

        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }
}
