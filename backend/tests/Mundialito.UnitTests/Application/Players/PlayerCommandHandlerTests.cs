using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Players.Commands.CreatePlayer;
using Mundialito.Application.Players.Commands.DeletePlayer;
using Mundialito.Application.Players.Commands.PatchPlayer;
using Mundialito.Application.Players.Commands.UpdatePlayer;
using Mundialito.Domain.Common;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;
using Mundialito.UnitTests.Infrastructure;
using NSubstitute;

namespace Mundialito.UnitTests.Application.Players;

public class PlayerCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly ITeamRepository _teams = Substitute.For<ITeamRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FixedClock _clock = new(Now);

    public PlayerCommandHandlerTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(1));
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_InExistingTeam_ShouldAddAndCommit()
    {
        var team = ExistingTeam();
        var handler = new CreatePlayerCommandHandler(_players, _teams, _unitOfWork, _clock);

        var result = await handler.Handle(
            new CreatePlayerCommand(team.Id, " Carlos ", "Gómez", 9, PlayerPosition.Forward, new DateOnly(1995, 5, 20)),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _players.Received(1).Add(Arg.Is<Player>(p => p.TeamId == team.Id && p.FirstName == "Carlos" && p.Id == result.Value));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_InMissingTeam_ShouldReturnTeamNotFound()
    {
        var teamId = Guid.CreateVersion7();
        _teams.GetByIdAsync(teamId, Arg.Any<CancellationToken>()).Returns((Team?)null);
        var handler = new CreatePlayerCommandHandler(_players, _teams, _unitOfWork, _clock);

        var result = await handler.Handle(
            new CreatePlayerCommand(teamId, "Carlos", "Gómez", 9, PlayerPosition.Forward, null), CancellationToken.None);

        result.Error.Code.ShouldBe("Team.NotFound");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        _players.DidNotReceive().Add(Arg.Any<Player>());
    }

    [Fact]
    public async Task Create_WithTakenJerseyNumber_ShouldConflict()
    {
        var team = ExistingTeam();
        _players.ExistsByJerseyNumberAsync(team.Id, 9, null, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreatePlayerCommandHandler(_players, _teams, _unitOfWork, _clock);

        var result = await handler.Handle(
            new CreatePlayerCommand(team.Id, "Carlos", "Gómez", 9, PlayerPosition.Forward, null), CancellationToken.None);

        result.Error.Code.ShouldBe("Player.JerseyNumberTaken");
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithFutureBirthDate_ShouldReturnDomainError()
    {
        var team = ExistingTeam();
        var handler = new CreatePlayerCommandHandler(_players, _teams, _unitOfWork, _clock);

        var result = await handler.Handle(
            new CreatePlayerCommand(team.Id, "Carlos", "Gómez", 9, PlayerPosition.Forward, Today.AddDays(1)), CancellationToken.None);

        result.Error.ShouldBe(PlayerErrors.BirthDateInFuture);
    }

    // ---------- Update (PUT) ----------

    [Fact]
    public async Task Update_ShouldReplaceFieldsExcludingSelfFromJerseyCheck()
    {
        var player = ExistingPlayer();
        var handler = new UpdatePlayerCommandHandler(_players, _unitOfWork, _clock);

        var result = await handler.Handle(
            new UpdatePlayerCommand(player.Id, "Luis", "Pérez", 1, PlayerPosition.Goalkeeper, null, IsActive: false),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        player.FullName.ShouldBe("Luis Pérez");
        player.JerseyNumber.ShouldBe(1);
        player.Position.ShouldBe(PlayerPosition.Goalkeeper);
        player.IsActive.ShouldBeFalse();
        await _players.Received(1).ExistsByJerseyNumberAsync(player.TeamId, 1, player.Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenMissing_ShouldReturnNotFound()
    {
        var id = Guid.CreateVersion7();
        _players.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Player?)null);
        var handler = new UpdatePlayerCommandHandler(_players, _unitOfWork, _clock);

        var result = await handler.Handle(
            new UpdatePlayerCommand(id, "Luis", "Pérez", 1, PlayerPosition.Goalkeeper, null, true), CancellationToken.None);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_WithStaleRowVersion_ShouldConflict()
    {
        var player = ExistingPlayer();
        var handler = new UpdatePlayerCommandHandler(_players, _unitOfWork, _clock);

        var result = await handler.Handle(
            new UpdatePlayerCommand(player.Id, "Luis", "Pérez", 1, PlayerPosition.Goalkeeper, null, true, ExpectedRowVersion: [7]),
            CancellationToken.None);

        result.Error.ShouldBe(PersistenceErrors.ConcurrencyConflict);
        player.FirstName.ShouldBe("Carlos");
    }

    [Fact]
    public async Task Update_WithTakenJerseyNumber_ShouldConflict()
    {
        var player = ExistingPlayer();
        _players.ExistsByJerseyNumberAsync(player.TeamId, 10, player.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new UpdatePlayerCommandHandler(_players, _unitOfWork, _clock);

        var result = await handler.Handle(
            new UpdatePlayerCommand(player.Id, "Carlos", "Gómez", 10, PlayerPosition.Forward, null, true), CancellationToken.None);

        result.Error.Code.ShouldBe("Player.JerseyNumberTaken");
        player.JerseyNumber.ShouldBe(9);
    }

    // ---------- Patch ----------

    [Fact]
    public async Task Patch_ShouldOnlyChangeProvidedFields()
    {
        var player = ExistingPlayer();
        var handler = new PatchPlayerCommandHandler(_players, _unitOfWork, _clock);

        var result = await handler.Handle(
            new PatchPlayerCommand(player.Id, null, null, null, PlayerPosition.Midfielder, null, null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        player.Position.ShouldBe(PlayerPosition.Midfielder);
        player.FullName.ShouldBe("Carlos Gómez");
        player.JerseyNumber.ShouldBe(9);
        await _players.DidNotReceive().ExistsByJerseyNumberAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Patch_WithNewJerseyNumber_ShouldCheckUniqueness()
    {
        var player = ExistingPlayer();
        _players.ExistsByJerseyNumberAsync(player.TeamId, 11, player.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new PatchPlayerCommandHandler(_players, _unitOfWork, _clock);

        var result = await handler.Handle(
            new PatchPlayerCommand(player.Id, null, null, 11, null, null, null), CancellationToken.None);

        result.Error.Code.ShouldBe("Player.JerseyNumberTaken");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_WhenMissing_ShouldSucceedWithoutCommit()
    {
        _players.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Player?)null);
        var handler = new DeletePlayerCommandHandler(_players, _unitOfWork);

        var result = await handler.Handle(new DeletePlayerCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WithGoals_ShouldConflict()
    {
        var player = ExistingPlayer();
        _players.HasGoalsAsync(player.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new DeletePlayerCommandHandler(_players, _unitOfWork);

        var result = await handler.Handle(new DeletePlayerCommand(player.Id), CancellationToken.None);

        result.Error.ShouldBe(PlayerErrors.HasGoals);
        _players.DidNotReceive().Remove(Arg.Any<Player>());
    }

    [Fact]
    public async Task Delete_WithoutGoals_ShouldRemoveAndCommit()
    {
        var player = ExistingPlayer();
        var handler = new DeletePlayerCommandHandler(_players, _unitOfWork);

        var result = await handler.Handle(new DeletePlayerCommand(player.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _players.Received(1).Remove(player);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private Team ExistingTeam()
    {
        var team = Team.Create("Finanzas", "FIN", null).Value;
        team.ClearDomainEvents();
        _teams.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        return team;
    }

    private Player ExistingPlayer()
    {
        var player = Player.Create(Guid.CreateVersion7(), "Carlos", "Gómez", 9, PlayerPosition.Forward, null, Today).Value;
        _players.GetByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
        return player;
    }
}
