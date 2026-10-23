using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Commands.CancelMatch;

internal sealed class CancelMatchCommandHandler(
    IMatchRepository matchRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelMatchCommand>
{
    public async Task<Result> Handle(CancelMatchCommand command, CancellationToken cancellationToken)
    {
        var match = await matchRepository.GetByIdAsync(command.Id, cancellationToken);
        if (match is null)
        {
            return Result.Failure(MatchErrors.NotFound(command.Id));
        }

        if (command.ExpectedRowVersion is not null && !match.HasVersion(command.ExpectedRowVersion))
        {
            return Result.Failure(PersistenceErrors.ConcurrencyConflict);
        }

        if (match.Status == MatchStatus.Cancelled)
        {
            return Result.Success();
        }

        var cancelResult = match.Cancel();
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }
}
