using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches.Commands.RescheduleMatch;

internal sealed class RescheduleMatchCommandHandler(
    IMatchRepository matchRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<RescheduleMatchCommand>
{
    public async Task<Result> Handle(RescheduleMatchCommand command, CancellationToken cancellationToken)
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

        var rescheduleResult = match.Reschedule(command.MatchDay, command.ScheduledAtUtc, command.Venue);
        if (rescheduleResult.IsFailure)
        {
            return rescheduleResult;
        }

        var commit = await unitOfWork.CommitAsync(cancellationToken);

        return commit.IsFailure ? Result.Failure(commit.Error) : Result.Success();
    }
}
