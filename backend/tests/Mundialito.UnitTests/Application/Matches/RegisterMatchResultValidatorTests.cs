using Mundialito.Application.Matches.Commands.RegisterMatchResult;

namespace Mundialito.UnitTests.Application.Matches;

public class RegisterMatchResultValidatorTests
{
    private readonly RegisterMatchResultCommandValidator _validator = new();

    [Fact]
    public void Validate_WithConsistentGoals_ShouldPass()
    {
        var command = new RegisterMatchResultCommand(Guid.CreateVersion7(), 2, 1,
        [
            new GoalInput(Guid.CreateVersion7(), 10),
            new GoalInput(Guid.CreateVersion7(), null),
            new GoalInput(Guid.CreateVersion7(), 90),
        ]);

        _validator.Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_GoallessDraw_ShouldPass()
    {
        _validator.Validate(new RegisterMatchResultCommand(Guid.CreateVersion7(), 0, 0, [])).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenGoalCountDiffersFromScore_ShouldFailOnGoals()
    {
        var command = new RegisterMatchResultCommand(Guid.CreateVersion7(), 2, 0, [new GoalInput(Guid.CreateVersion7(), 5)]);

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Goals");
    }

    [Fact]
    public void Validate_WithNegativeScore_ShouldFail()
    {
        var result = _validator.Validate(new RegisterMatchResultCommand(Guid.CreateVersion7(), -1, 0, []));

        result.Errors.ShouldContain(e => e.PropertyName == "HomeScore");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Validate_WithMinuteOutOfRange_ShouldFailOnThatGoal(int minute)
    {
        var command = new RegisterMatchResultCommand(Guid.CreateVersion7(), 1, 0, [new GoalInput(Guid.CreateVersion7(), minute)]);

        var result = _validator.Validate(command);

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Goals[0].Minute");
    }

    [Fact]
    public void Validate_WithEmptyPlayerId_ShouldFailOnThatGoal()
    {
        var command = new RegisterMatchResultCommand(Guid.CreateVersion7(), 1, 0, [new GoalInput(Guid.Empty, 10)]);

        _validator.Validate(command).Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Goals[0].PlayerId");
    }
}
