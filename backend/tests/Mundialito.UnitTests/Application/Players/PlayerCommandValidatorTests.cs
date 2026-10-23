using Mundialito.Application.Players.Commands.CreatePlayer;
using Mundialito.Application.Players.Commands.PatchPlayer;
using Mundialito.Application.Players.Commands.UpdatePlayer;
using Mundialito.Domain.Players;

namespace Mundialito.UnitTests.Application.Players;

public class PlayerCommandValidatorTests
{
    private static readonly Guid TeamId = Guid.CreateVersion7();

    private readonly CreatePlayerCommandValidator _create = new();
    private readonly UpdatePlayerCommandValidator _update = new();
    private readonly PatchPlayerCommandValidator _patch = new();

    [Fact]
    public void Create_WithValidData_ShouldPass()
    {
        var command = new CreatePlayerCommand(TeamId, "Carlos", "Gómez", 9, PlayerPosition.Forward, null);

        _create.Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithSeveralProblems_ShouldReportEachField()
    {
        var command = new CreatePlayerCommand(Guid.Empty, "", "X", 0, (PlayerPosition)42, null);

        var result = _create.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).Distinct()
            .ShouldBe(["TeamId", "FirstName", "JerseyNumber", "Position"], ignoreOrder: true);
    }

    [Fact]
    public void Update_WithEmptyId_ShouldFail()
    {
        var command = new UpdatePlayerCommand(Guid.Empty, "Carlos", "Gómez", 9, PlayerPosition.Forward, null, true);

        _update.Validate(command).Errors.ShouldContain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Patch_WithNoFields_ShouldFail()
    {
        var command = new PatchPlayerCommand(Guid.CreateVersion7(), null, null, null, null, null, null);

        _patch.Validate(command).Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("body");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Patch_WithJerseyOutOfRange_ShouldFail(int jersey)
    {
        var command = new PatchPlayerCommand(Guid.CreateVersion7(), null, null, jersey, null, null, null);

        _patch.Validate(command).Errors.ShouldContain(e => e.PropertyName == "JerseyNumber");
    }

    [Fact]
    public void Patch_WithOnlyPosition_ShouldPass()
    {
        var command = new PatchPlayerCommand(Guid.CreateVersion7(), null, null, null, PlayerPosition.Defender, null, null);

        _patch.Validate(command).IsValid.ShouldBeTrue();
    }
}
