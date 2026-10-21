using Mundialito.Domain.Teams;
using Mundialito.Domain.Teams.Events;

namespace Mundialito.UnitTests.Domain.Teams;

public class TeamTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceedAndRaiseEvent()
    {
        var result = Team.Create("  Finanzas FC ", " fin ", "  Ana Pérez ");

        result.IsSuccess.ShouldBeTrue();
        var team = result.Value;
        team.Id.ShouldNotBe(Guid.Empty);
        team.Name.ShouldBe("Finanzas FC");
        team.Code.ShouldBe("FIN");
        team.Coach.ShouldBe("Ana Pérez");
        team.IsActive.ShouldBeTrue();

        var domainEvent = team.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TeamCreatedDomainEvent>();
        domainEvent.TeamId.ShouldBe(team.Id);
        domainEvent.Code.ShouldBe("FIN");
    }

    [Fact]
    public void Create_WithBlankCoach_ShouldStoreNull()
    {
        var team = Team.Create("Logística", "LOG", "   ").Value;

        team.Coach.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_ShouldFail(string? name)
    {
        var result = Team.Create(name, "FIN", null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TeamErrors.NameRequired);
    }

    [Fact]
    public void Create_WithNameTooLong_ShouldFail()
    {
        var result = Team.Create(new string('a', Team.NameMaxLength + 1), "FIN", null);

        result.Error.ShouldBe(TeamErrors.NameTooLong);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("FI N")]
    [InlineData("FI-N")]
    public void Create_WithInvalidCode_ShouldFail(string code)
    {
        var result = Team.Create("Finanzas", code, null);

        result.Error.ShouldBe(TeamErrors.CodeInvalid);
    }

    [Fact]
    public void Update_ShouldReplaceAllEditableFields()
    {
        var team = Team.Create("Finanzas", "FIN", "Ana").Value;

        var result = team.Update("Contabilidad", "con", null);

        result.IsSuccess.ShouldBeTrue();
        team.Name.ShouldBe("Contabilidad");
        team.Code.ShouldBe("CON");
        team.Coach.ShouldBeNull();
    }

    [Fact]
    public void Update_WithInvalidData_ShouldNotChangeState()
    {
        var team = Team.Create("Finanzas", "FIN", "Ana").Value;

        var result = team.Update("", "FIN", "Ana");

        result.Error.ShouldBe(TeamErrors.NameRequired);
        team.Name.ShouldBe("Finanzas");
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var team = Team.Create("Finanzas", "FIN", null).Value;

        team.Deactivate();

        team.IsActive.ShouldBeFalse();
    }
}
