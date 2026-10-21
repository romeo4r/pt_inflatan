using Mundialito.Domain.Matches;
using Mundialito.Domain.Matches.Events;

namespace Mundialito.UnitTests.Domain.Matches;

public class MatchTests
{
    private static readonly Guid HomeTeamId = Guid.CreateVersion7();
    private static readonly Guid AwayTeamId = Guid.CreateVersion7();
    private static readonly Guid OtherTeamId = Guid.CreateVersion7();
    private static readonly DateTime ScheduledAt = new(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FinishedAt = new(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc);

    private static Match ScheduledMatch() =>
        Match.Schedule(HomeTeamId, AwayTeamId, 1, ScheduledAt, " Cancha Principal ").Value;

    private static GoalEntry HomeGoal(int? minute = 10) => new(Guid.CreateVersion7(), HomeTeamId, minute);

    private static GoalEntry AwayGoal(int? minute = 20) => new(Guid.CreateVersion7(), AwayTeamId, minute);

    [Fact]
    public void Schedule_WithValidData_ShouldCreateScheduledMatch()
    {
        var match = ScheduledMatch();

        match.Status.ShouldBe(MatchStatus.Scheduled);
        match.HomeTeamId.ShouldBe(HomeTeamId);
        match.AwayTeamId.ShouldBe(AwayTeamId);
        match.MatchDay.ShouldBe(1);
        match.Venue.ShouldBe("Cancha Principal");
        match.HomeScore.ShouldBeNull();
        match.AwayScore.ShouldBeNull();
        match.Goals.ShouldBeEmpty();
        match.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Schedule_WithSameTeam_ShouldFail()
    {
        var result = Match.Schedule(HomeTeamId, HomeTeamId, 1, ScheduledAt, null);

        result.Error.ShouldBe(MatchErrors.SameTeam);
    }

    [Fact]
    public void Schedule_WithMatchDayBelowOne_ShouldFail()
    {
        var result = Match.Schedule(HomeTeamId, AwayTeamId, 0, ScheduledAt, null);

        result.Error.ShouldBe(MatchErrors.MatchDayInvalid);
    }

    [Fact]
    public void RegisterResult_WithConsistentGoals_ShouldFinishMatchAndRaiseEvent()
    {
        var match = ScheduledMatch();
        var goals = new[] { HomeGoal(12), HomeGoal(70), AwayGoal(45) };

        var result = match.RegisterResult(2, 1, goals, FinishedAt);

        result.IsSuccess.ShouldBeTrue();
        match.Status.ShouldBe(MatchStatus.Finished);
        match.HomeScore.ShouldBe(2);
        match.AwayScore.ShouldBe(1);
        match.FinishedAtUtc.ShouldBe(FinishedAt);
        match.Goals.Count.ShouldBe(3);
        match.Goals.ShouldAllBe(g => g.MatchId == match.Id);

        var domainEvent = match.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<MatchResultRegisteredDomainEvent>();
        domainEvent.MatchId.ShouldBe(match.Id);
        domainEvent.HomeScore.ShouldBe(2);
        domainEvent.AwayScore.ShouldBe(1);
    }

    [Fact]
    public void RegisterResult_GoallessDraw_ShouldSucceedWithoutGoals()
    {
        var match = ScheduledMatch();

        var result = match.RegisterResult(0, 0, [], FinishedAt);

        result.IsSuccess.ShouldBeTrue();
        match.Status.ShouldBe(MatchStatus.Finished);
        match.Goals.ShouldBeEmpty();
    }

    [Fact]
    public void RegisterResult_WhenGoalsDoNotMatchScore_ShouldFail()
    {
        var match = ScheduledMatch();

        var result = match.RegisterResult(2, 0, [HomeGoal()], FinishedAt);

        result.Error.ShouldBe(MatchErrors.GoalsDoNotMatchScore);
        match.Status.ShouldBe(MatchStatus.Scheduled);
        match.Goals.ShouldBeEmpty();
    }

    [Fact]
    public void RegisterResult_WithGoalFromForeignTeam_ShouldFail()
    {
        var match = ScheduledMatch();
        var foreignGoal = new GoalEntry(Guid.CreateVersion7(), OtherTeamId, 5);

        var result = match.RegisterResult(1, 0, [foreignGoal], FinishedAt);

        result.Error.ShouldBe(MatchErrors.ScorerTeamNotInMatch);
    }

    [Fact]
    public void RegisterResult_WithNegativeScore_ShouldFail()
    {
        var match = ScheduledMatch();

        var result = match.RegisterResult(-1, 0, [], FinishedAt);

        result.Error.ShouldBe(MatchErrors.NegativeScore);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void RegisterResult_WithMinuteOutOfRange_ShouldFail(int minute)
    {
        var match = ScheduledMatch();

        var result = match.RegisterResult(1, 0, [HomeGoal(minute)], FinishedAt);

        result.Error.ShouldBe(MatchErrors.MinuteOutOfRange);
    }

    [Fact]
    public void RegisterResult_Twice_ShouldFailWithAlreadyFinished()
    {
        var match = ScheduledMatch();
        match.RegisterResult(0, 0, [], FinishedAt);

        var result = match.RegisterResult(1, 0, [HomeGoal()], FinishedAt);

        result.Error.ShouldBe(MatchErrors.AlreadyFinished);
        match.DomainEvents.Count.ShouldBe(1);
    }

    [Fact]
    public void RegisterResult_OnCancelledMatch_ShouldFail()
    {
        var match = ScheduledMatch();
        match.Cancel();

        var result = match.RegisterResult(0, 0, [], FinishedAt);

        result.Error.ShouldBe(MatchErrors.Cancelled);
    }

    [Fact]
    public void Cancel_OnFinishedMatch_ShouldFail()
    {
        var match = ScheduledMatch();
        match.RegisterResult(0, 0, [], FinishedAt);

        var result = match.Cancel();

        result.Error.ShouldBe(MatchErrors.AlreadyFinished);
    }

    [Fact]
    public void Cancel_Twice_ShouldBeIdempotent()
    {
        var match = ScheduledMatch();

        match.Cancel().IsSuccess.ShouldBeTrue();
        match.Cancel().IsSuccess.ShouldBeTrue();
        match.Status.ShouldBe(MatchStatus.Cancelled);
    }

    [Fact]
    public void Reschedule_OnScheduledMatch_ShouldUpdateFixture()
    {
        var match = ScheduledMatch();
        var newDate = ScheduledAt.AddDays(7);

        var result = match.Reschedule(2, newDate, null);

        result.IsSuccess.ShouldBeTrue();
        match.MatchDay.ShouldBe(2);
        match.ScheduledAtUtc.ShouldBe(newDate);
        match.Venue.ShouldBeNull();
    }

    [Fact]
    public void Reschedule_OnFinishedMatch_ShouldFail()
    {
        var match = ScheduledMatch();
        match.RegisterResult(0, 0, [], FinishedAt);

        var result = match.Reschedule(2, ScheduledAt, null);

        result.Error.ShouldBe(MatchErrors.NotScheduled);
    }
}
