using Mundialito.Application.Teams.Commands.CreateTeam;
using Mundialito.Application.Teams.Commands.PatchTeam;
using Mundialito.Application.Teams.Commands.UpdateTeam;

namespace Mundialito.UnitTests.Application.Teams;

public class TeamCommandValidatorTests
{
    private readonly CreateTeamCommandValidator _create = new();
    private readonly UpdateTeamCommandValidator _update = new();
    private readonly PatchTeamCommandValidator _patch = new();

    [Fact]
    public void Create_WithValidData_ShouldPass()
    {
        _create.Validate(new CreateTeamCommand("Finanzas", "FIN", "Ana")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithEmptyNameAndBadCode_ShouldReportBothFields()
    {
        var result = _create.Validate(new CreateTeamCommand("", "F-1", null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).Distinct().ShouldBe(["Name", "Code"], ignoreOrder: true);
        result.Errors.ShouldAllBe(e => e.ErrorMessage.Length > 0);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("FI N")]
    public void Create_WithInvalidCode_ShouldFail(string code)
    {
        _create.Validate(new CreateTeamCommand("Finanzas", code, null)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Update_WithEmptyId_ShouldFail()
    {
        var result = _update.Validate(new UpdateTeamCommand(Guid.Empty, "Finanzas", "FIN", null, true));

        result.Errors.ShouldContain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Patch_WithNoFields_ShouldFail()
    {
        var result = _patch.Validate(new PatchTeamCommand(Guid.CreateVersion7(), null, null, null, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("body");
    }

    [Fact]
    public void Patch_WithOnlyIsActive_ShouldPass()
    {
        _patch.Validate(new PatchTeamCommand(Guid.CreateVersion7(), null, null, null, false)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Patch_WithEmptyName_ShouldFailButEmptyCoachIsAllowed()
    {
        var result = _patch.Validate(new PatchTeamCommand(Guid.CreateVersion7(), "", null, "", null));

        result.Errors.Select(e => e.PropertyName).ShouldBe(["Name"]);
    }
}
