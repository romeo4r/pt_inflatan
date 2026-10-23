using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Commands.DeleteMatch;

internal sealed class DeleteMatchCommandHandler(
    IMatchRepository matchRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteMatchCommand>
{
    public async Task<Result> Handle(DeleteMatchCommand command, CancellationToken cancellationToken)
    {
        var match = await matchRepository.GetByIdAsync(command.Id, cancellationToken);
        if (match is null)
        {
            return Result.Success();
        }

        if (match.Status == MatchStatus.Finished)
        {
            return Result.Failure(MatchErrors.AlreadyFinished);
        }

        matchRepository.Remove(match);

        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }
}
