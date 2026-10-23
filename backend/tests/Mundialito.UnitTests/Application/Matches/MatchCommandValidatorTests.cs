using Mundialito.Application.Matches.Commands.PatchMatch;
using Mundialito.Application.Matches.Commands.RescheduleMatch;
using Mundialito.Application.Matches.Commands.ScheduleMatch;

namespace Mundialito.UnitTests.Application.Matches;

public class MatchCommandValidatorTests
{
    private static readonly DateTime KickOff = new(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Home = Guid.CreateVersion7();
    private static readonly Guid Away = Guid.CreateVersion7();

    private readonly ScheduleMatchCommandValidator _schedule = new();
    private readonly RescheduleMatchCommandValidator _reschedule = new();
    private readonly PatchMatchCommandValidator _patch = new();

    [Fact]
    public void Schedule_WithValidData_ShouldPass()
    {
        _schedule.Validate(new ScheduleMatchCommand(Home, Away, 1, KickOff, "Cancha 1")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Schedule_WithSameTeamAndMissingDate_ShouldReportBoth()
    {
        var result = _schedule.Validate(new ScheduleMatchCommand(Home, Home, 0, default, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).Distinct()
            .ShouldBe(["AwayTeamId", "MatchDay", "ScheduledAtUtc"], ignoreOrder: true);
    }

    [Fact]
    public void Schedule_WithVenueTooLong_ShouldFail()
    {
        var result = _schedule.Validate(new ScheduleMatchCommand(Home, Away, 1, KickOff, new string('x', 151)));

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Venue");
    }

    [Fact]
    public void Reschedule_WithEmptyId_ShouldFail()
    {
        _reschedule.Validate(new RescheduleMatchCommand(Guid.Empty, 1, KickOff, null))
            .Errors.ShouldContain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Patch_WithNoFields_ShouldFail()
    {
        _patch.Validate(new PatchMatchCommand(Guid.CreateVersion7(), null, null, null))
            .Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("body");
    }

    [Fact]
    public void Patch_WithMatchDayZero_ShouldFailOnMatchDay()
    {
        _patch.Validate(new PatchMatchCommand(Guid.CreateVersion7(), 0, null, null))
            .Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("MatchDay");
    }

    [Fact]
    public void Patch_WithOnlyVenue_ShouldPass()
    {
        _patch.Validate(new PatchMatchCommand(Guid.CreateVersion7(), null, null, "")).IsValid.ShouldBeTrue();
    }
}
