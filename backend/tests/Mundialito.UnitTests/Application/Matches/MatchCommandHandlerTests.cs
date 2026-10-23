using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Matches.Commands.CancelMatch;
using Mundialito.Application.Matches.Commands.DeleteMatch;
using Mundialito.Application.Matches.Commands.PatchMatch;
using Mundialito.Application.Matches.Commands.RescheduleMatch;
using Mundialito.Application.Matches.Commands.ScheduleMatch;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Teams;
using NSubstitute;

namespace Mundialito.UnitTests.Application.Matches;

public class MatchCommandHandlerTests
{
    private static readonly DateTime KickOff = new(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);

    private readonly IMatchRepository _matches = Substitute.For<IMatchRepository>();
    private readonly ITeamRepository _teams = Substitute.For<ITeamRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public MatchCommandHandlerTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(1));
    }

    // ---------- Schedule ----------

    [Fact]
    public async Task Schedule_WithTwoExistingTeams_ShouldAddAndCommit()
    {
        var home = ExistingTeam("Finanzas", "FIN");
        var away = ExistingTeam("Tecnología", "TEC");
        var handler = new ScheduleMatchCommandHandler(_matches, _teams, _unitOfWork);

        var result = await handler.Handle(
            new ScheduleMatchCommand(home.Id, away.Id, 1, KickOff, " Cancha 1 "), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _matches.Received(1).Add(Arg.Is<Match>(m =>
            m.Id == result.Value && m.HomeTeamId == home.Id && m.AwayTeamId == away.Id
            && m.Status == MatchStatus.Scheduled && m.Venue == "Cancha 1"));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Schedule_WithUnknownAwayTeam_ShouldReturnTeamNotFound()
    {
        var home = ExistingTeam("Finanzas", "FIN");
        var missing = Guid.CreateVersion7();
        _teams.GetByIdAsync(missing, Arg.Any<CancellationToken>()).Returns((Team?)null);
        var handler = new ScheduleMatchCommandHandler(_matches, _teams, _unitOfWork);

        var result = await handler.Handle(
            new ScheduleMatchCommand(home.Id, missing, 1, KickOff, null), CancellationToken.None);

        result.Error.Code.ShouldBe("Team.NotFound");
        result.Error.Message.ShouldContain(missing.ToString());
        _matches.DidNotReceive().Add(Arg.Any<Match>());
    }

    [Fact]
    public async Task Schedule_WithSameTeamTwice_ShouldReturnDomainError()
    {
        var team = ExistingTeam("Finanzas", "FIN");
        var handler = new ScheduleMatchCommandHandler(_matches, _teams, _unitOfWork);

        var result = await handler.Handle(
            new ScheduleMatchCommand(team.Id, team.Id, 1, KickOff, null), CancellationToken.None);

        result.Error.ShouldBe(MatchErrors.SameTeam);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Reschedule (PUT) ----------

    [Fact]
    public async Task Reschedule_OnScheduledMatch_ShouldReplaceFixtureAndCommit()
    {
        var match = ExistingMatch();
        var handler = new RescheduleMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(
            new RescheduleMatchCommand(match.Id, 3, KickOff.AddDays(14), null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        match.MatchDay.ShouldBe(3);
        match.ScheduledAtUtc.ShouldBe(KickOff.AddDays(14));
        match.Venue.ShouldBeNull();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reschedule_OnFinishedMatch_ShouldConflict()
    {
        var match = ExistingMatch();
        match.RegisterResult(0, 0, [], KickOff.AddHours(2));
        var handler = new RescheduleMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(
            new RescheduleMatchCommand(match.Id, 3, KickOff.AddDays(14), null), CancellationToken.None);

        result.Error.ShouldBe(MatchErrors.NotScheduled);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Reschedule_WithStaleRowVersion_ShouldConflict()
    {
        var match = ExistingMatch();
        var handler = new RescheduleMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(
            new RescheduleMatchCommand(match.Id, 3, KickOff, null, ExpectedRowVersion: [1]), CancellationToken.None);

        result.Error.ShouldBe(PersistenceErrors.ConcurrencyConflict);
        match.MatchDay.ShouldBe(1);
    }

    [Fact]
    public async Task Reschedule_WhenMissing_ShouldReturnNotFound()
    {
        var id = Guid.CreateVersion7();
        _matches.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Match?)null);
        var handler = new RescheduleMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(new RescheduleMatchCommand(id, 1, KickOff, null), CancellationToken.None);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    // ---------- Patch ----------

    [Fact]
    public async Task Patch_ShouldOnlyChangeProvidedFields()
    {
        var match = ExistingMatch();
        var handler = new PatchMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(
            new PatchMatchCommand(match.Id, null, null, "Polideportivo"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        match.Venue.ShouldBe("Polideportivo");
        match.MatchDay.ShouldBe(1);
        match.ScheduledAtUtc.ShouldBe(KickOff);
    }

    [Fact]
    public async Task Patch_WithEmptyVenue_ShouldClearIt()
    {
        var match = ExistingMatch();
        var handler = new PatchMatchCommandHandler(_matches, _unitOfWork);

        await handler.Handle(new PatchMatchCommand(match.Id, null, null, ""), CancellationToken.None);

        match.Venue.ShouldBeNull();
    }

    // ---------- Cancel ----------

    [Fact]
    public async Task Cancel_OnScheduledMatch_ShouldCancelAndCommit()
    {
        var match = ExistingMatch();
        var handler = new CancelMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(new CancelMatchCommand(match.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        match.Status.ShouldBe(MatchStatus.Cancelled);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_OnAlreadyCancelledMatch_ShouldSucceedWithoutCommit()
    {
        var match = ExistingMatch();
        match.Cancel();
        var handler = new CancelMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(new CancelMatchCommand(match.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_OnFinishedMatch_ShouldConflict()
    {
        var match = ExistingMatch();
        match.RegisterResult(0, 0, [], KickOff.AddHours(2));
        var handler = new CancelMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(new CancelMatchCommand(match.Id), CancellationToken.None);

        result.Error.ShouldBe(MatchErrors.AlreadyFinished);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_WhenMissing_ShouldSucceedWithoutCommit()
    {
        _matches.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Match?)null);
        var handler = new DeleteMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(new DeleteMatchCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_OnFinishedMatch_ShouldConflict()
    {
        var match = ExistingMatch();
        match.RegisterResult(0, 0, [], KickOff.AddHours(2));
        var handler = new DeleteMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(new DeleteMatchCommand(match.Id), CancellationToken.None);

        result.Error.ShouldBe(MatchErrors.AlreadyFinished);
        _matches.DidNotReceive().Remove(Arg.Any<Match>());
    }

    [Fact]
    public async Task Delete_OnScheduledOrCancelledMatch_ShouldRemoveAndCommit()
    {
        var match = ExistingMatch();
        match.Cancel();
        var handler = new DeleteMatchCommandHandler(_matches, _unitOfWork);

        var result = await handler.Handle(new DeleteMatchCommand(match.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _matches.Received(1).Remove(match);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private Team ExistingTeam(string name, string code)
    {
        var team = Team.Create(name, code, null).Value;
        team.ClearDomainEvents();
        _teams.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        return team;
    }

    private Match ExistingMatch()
    {
        var match = Match.Schedule(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, KickOff, "Cancha 1").Value;
        _matches.GetByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
        return match;
    }
}
