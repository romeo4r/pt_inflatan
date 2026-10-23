using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Commands.PatchMatch;

internal sealed class PatchMatchCommandHandler(
    IMatchRepository matchRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<PatchMatchCommand>
{
    public async Task<Result> Handle(PatchMatchCommand command, CancellationToken cancellationToken)
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

        // Merge: a null member means "keep the current value".
        var rescheduleResult = match.Reschedule(
            command.MatchDay ?? match.MatchDay,
            command.ScheduledAtUtc ?? match.ScheduledAtUtc,
            command.Venue ?? match.Venue);

        if (rescheduleResult.IsFailure)
        {
            return rescheduleResult;
        }

        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }
}
