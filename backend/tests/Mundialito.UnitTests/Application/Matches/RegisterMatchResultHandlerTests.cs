using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Matches.Commands.RegisterMatchResult;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Matches.Events;
using Mundialito.Domain.Players;
using Mundialito.UnitTests.Infrastructure;
using NSubstitute;

namespace Mundialito.UnitTests.Application.Matches;

public class RegisterMatchResultHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime KickOff = Now.AddHours(-2);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly IMatchRepository _matches = Substitute.For<IMatchRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FixedClock _clock = new(Now);

    private readonly Guid _homeTeamId = Guid.CreateVersion7();
    private readonly Guid _awayTeamId = Guid.CreateVersion7();
    private readonly Match _match;
    private readonly Player _homeScorer;
    private readonly Player _awayScorer;
    private readonly Player _outsider;

    public RegisterMatchResultHandlerTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(1));

        _match = Match.Schedule(_homeTeamId, _awayTeamId, 1, KickOff, null).Value;
        _matches.GetByIdAsync(_match.Id, Arg.Any<CancellationToken>()).Returns(_match);

        _homeScorer = Player.Create(_homeTeamId, "Carlos", "Gómez", 9, PlayerPosition.Forward, null, Today).Value;
        _awayScorer = Player.Create(_awayTeamId, "Luis", "Pérez", 7, PlayerPosition.Forward, null, Today).Value;
        _outsider = Player.Create(Guid.CreateVersion7(), "Ajeno", "Equipo", 5, PlayerPosition.Defender, null, Today).Value;

        _players.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ids = call.Arg<IEnumerable<Guid>>().ToHashSet();
                return new[] { _homeScorer, _awayScorer, _outsider }.Where(p => ids.Contains(p.Id)).ToList();
            });
    }

    [Fact]
    public async Task Handle_WithGoalsFromBothTeams_ShouldFinishMatchCreditingEachScorersTeam()
    {
        var handler = CreateHandler();
        var command = new RegisterMatchResultCommand(_match.Id, 2, 1,
        [
            new GoalInput(_homeScorer.Id, 10),
            new GoalInput(_homeScorer.Id, 75),
            new GoalInput(_awayScorer.Id, 44),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _match.Status.ShouldBe(MatchStatus.Finished);
        _match.HomeScore.ShouldBe(2);
        _match.AwayScore.ShouldBe(1);
        _match.FinishedAtUtc.ShouldBe(Now);
        _match.Goals.Count(g => g.TeamId == _homeTeamId).ShouldBe(2);
        _match.Goals.Single(g => g.TeamId == _awayTeamId).PlayerId.ShouldBe(_awayScorer.Id);
        _match.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<MatchResultRegisteredDomainEvent>();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GoallessDraw_ShouldNotQueryPlayers()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new RegisterMatchResultCommand(_match.Id, 0, 0, []), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _match.Status.ShouldBe(MatchStatus.Finished);
        await _players.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithScorerFromThirdTeam_ShouldRejectAndLeaveMatchScheduled()
    {
        var handler = CreateHandler();
        var command = new RegisterMatchResultCommand(_match.Id, 1, 0, [new GoalInput(_outsider.Id, 20)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Error.Code.ShouldBe("Player.NotInTeam");
        result.Error.Type.ShouldBe(ErrorType.Validation);
        _match.Status.ShouldBe(MatchStatus.Scheduled);
        _match.Goals.ShouldBeEmpty();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUnknownPlayer_ShouldReturnPlayerNotFound()
    {
        var handler = CreateHandler();
        var ghost = Guid.CreateVersion7();
        var command = new RegisterMatchResultCommand(_match.Id, 1, 0, [new GoalInput(ghost, null)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Error.Code.ShouldBe("Player.NotFound");
        result.Error.Message.ShouldContain(ghost.ToString());
    }

    [Fact]
    public async Task Handle_WhenGoalsPerTeamDoNotMatchScore_ShouldReturnDomainError()
    {
        var handler = CreateHandler();
        // Total count matches (1) but the goal belongs to the away team while the score says home.
        var command = new RegisterMatchResultCommand(_match.Id, 1, 0, [new GoalInput(_awayScorer.Id, 30)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Error.ShouldBe(MatchErrors.GoalsDoNotMatchScore);
        _match.Status.ShouldBe(MatchStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_OnFinishedMatch_ShouldConflictWithoutLoadingPlayers()
    {
        _match.RegisterResult(0, 0, [], Now);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RegisterMatchResultCommand(_match.Id, 1, 0, [new GoalInput(_homeScorer.Id, 1)]), CancellationToken.None);

        result.Error.ShouldBe(MatchErrors.AlreadyFinished);
        await _players.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnCancelledMatch_ShouldConflict()
    {
        _match.Cancel();
        var handler = CreateHandler();

        var result = await handler.Handle(new RegisterMatchResultCommand(_match.Id, 0, 0, []), CancellationToken.None);

        result.Error.ShouldBe(MatchErrors.Cancelled);
    }

    [Fact]
    public async Task Handle_WithStaleRowVersion_ShouldConflict()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RegisterMatchResultCommand(_match.Id, 0, 0, [], ExpectedRowVersion: [1, 2]), CancellationToken.None);

        result.Error.ShouldBe(PersistenceErrors.ConcurrencyConflict);
        _match.Status.ShouldBe(MatchStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_WhenMissing_ShouldReturnNotFound()
    {
        var id = Guid.CreateVersion7();
        _matches.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Match?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new RegisterMatchResultCommand(id, 0, 0, []), CancellationToken.None);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCommitReportsConflict_ShouldPropagateIt()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure<int>(PersistenceErrors.ConcurrencyConflict));
        var handler = CreateHandler();

        var result = await handler.Handle(new RegisterMatchResultCommand(_match.Id, 0, 0, []), CancellationToken.None);

        result.Error.ShouldBe(PersistenceErrors.ConcurrencyConflict);
    }

    private RegisterMatchResultCommandHandler CreateHandler() => new(_matches, _players, _unitOfWork, _clock);
}
