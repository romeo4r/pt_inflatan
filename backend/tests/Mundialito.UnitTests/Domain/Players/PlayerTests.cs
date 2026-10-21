using Mundialito.Domain.Players;

namespace Mundialito.UnitTests.Domain.Players;

public class PlayerTests
{
    private static readonly Guid TeamId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 10, 21);

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var result = Player.Create(TeamId, " Carlos ", " Gómez ", 9, PlayerPosition.Forward, new DateOnly(1995, 5, 20), Today);

        result.IsSuccess.ShouldBeTrue();
        var player = result.Value;
        player.TeamId.ShouldBe(TeamId);
        player.FirstName.ShouldBe("Carlos");
        player.LastName.ShouldBe("Gómez");
        player.FullName.ShouldBe("Carlos Gómez");
        player.JerseyNumber.ShouldBe(9);
        player.Position.ShouldBe(PlayerPosition.Forward);
        player.IsActive.ShouldBeTrue();
        player.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WithoutFirstName_ShouldFail()
    {
        var result = Player.Create(TeamId, " ", "Gómez", 9, PlayerPosition.Forward, null, Today);

        result.Error.ShouldBe(PlayerErrors.FirstNameRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_WithJerseyNumberOutOfRange_ShouldFail(int jerseyNumber)
    {
        var result = Player.Create(TeamId, "Carlos", "Gómez", jerseyNumber, PlayerPosition.Forward, null, Today);

        result.Error.ShouldBe(PlayerErrors.JerseyNumberOutOfRange);
    }

    [Fact]
    public void Create_WithUndefinedPosition_ShouldFail()
    {
        var result = Player.Create(TeamId, "Carlos", "Gómez", 9, (PlayerPosition)99, null, Today);

        result.Error.ShouldBe(PlayerErrors.PositionInvalid);
    }

    [Fact]
    public void Create_WithFutureBirthDate_ShouldFail()
    {
        var result = Player.Create(TeamId, "Carlos", "Gómez", 9, PlayerPosition.Forward, Today.AddDays(1), Today);

        result.Error.ShouldBe(PlayerErrors.BirthDateInFuture);
    }

    [Fact]
    public void Update_ShouldReplaceEditableFieldsButKeepTeam()
    {
        var player = Player.Create(TeamId, "Carlos", "Gómez", 9, PlayerPosition.Forward, null, Today).Value;

        var result = player.Update("Luis", "Pérez", 1, PlayerPosition.Goalkeeper, null, Today);

        result.IsSuccess.ShouldBeTrue();
        player.FullName.ShouldBe("Luis Pérez");
        player.JerseyNumber.ShouldBe(1);
        player.Position.ShouldBe(PlayerPosition.Goalkeeper);
        player.TeamId.ShouldBe(TeamId);
    }
}
