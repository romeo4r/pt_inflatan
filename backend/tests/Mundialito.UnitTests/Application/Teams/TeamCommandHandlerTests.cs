using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Teams.Commands.CreateTeam;
using Mundialito.Application.Teams.Commands.DeleteTeam;
using Mundialito.Application.Teams.Commands.PatchTeam;
using Mundialito.Application.Teams.Commands.UpdateTeam;
using Mundialito.Domain.Common;
using Mundialito.Domain.Teams;
using NSubstitute;

namespace Mundialito.UnitTests.Application.Teams;

public class TeamCommandHandlerTests
{
    private readonly ITeamRepository _teams = Substitute.For<ITeamRepository>();
    private readonly IMatchRepository _matches = Substitute.For<IMatchRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    // ---------- Create ----------

    [Fact]
    public async Task Create_WithUniqueData_ShouldAddAndCommit()
    {
        var handler = new CreateTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(new CreateTeamCommand(" Finanzas ", "fin", null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
        _teams.Received(1).Add(Arg.Is<Team>(t => t.Name == "Finanzas" && t.Code == "FIN" && t.Id == result.Value));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithDuplicateName_ShouldConflictWithoutCommit()
    {
        _teams.ExistsByNameAsync("Finanzas", null, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(new CreateTeamCommand("Finanzas", "FIN", null), CancellationToken.None);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Team.NameAlreadyExists");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithDuplicateCode_ShouldConflictUsingNormalizedCode()
    {
        _teams.ExistsByCodeAsync("FIN", null, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(new CreateTeamCommand("Finanzas", " fin ", null), CancellationToken.None);

        result.Error.Code.ShouldBe("Team.CodeAlreadyExists");
        _teams.DidNotReceive().Add(Arg.Any<Team>());
    }

    [Fact]
    public async Task Create_WhenDomainRejects_ShouldReturnDomainError()
    {
        var handler = new CreateTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(new CreateTeamCommand("Finanzas", "F", null), CancellationToken.None);

        result.Error.ShouldBe(TeamErrors.CodeInvalid);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Update (PUT) ----------

    [Fact]
    public async Task Update_WhenMissing_ShouldReturnNotFound()
    {
        var id = Guid.CreateVersion7();
        _teams.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Team?)null);
        var handler = new UpdateTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(new UpdateTeamCommand(id, "X", "XX", null, true), CancellationToken.None);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_ShouldReplaceAllFieldsAndCommit()
    {
        var team = ExistingTeam("Finanzas", "FIN");
        var handler = new UpdateTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(
            new UpdateTeamCommand(team.Id, "Contabilidad", "con", "Ana", IsActive: false), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        team.Name.ShouldBe("Contabilidad");
        team.Code.ShouldBe("CON");
        team.Coach.ShouldBe("Ana");
        team.IsActive.ShouldBeFalse();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ShouldExcludeItselfFromUniquenessCheck()
    {
        var team = ExistingTeam("Finanzas", "FIN");
        var handler = new UpdateTeamCommandHandler(_teams, _unitOfWork);

        await handler.Handle(new UpdateTeamCommand(team.Id, "Finanzas", "FIN", null, true), CancellationToken.None);

        await _teams.Received(1).ExistsByNameAsync("Finanzas", team.Id, Arg.Any<CancellationToken>());
        await _teams.Received(1).ExistsByCodeAsync("FIN", team.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithNameTakenByAnotherTeam_ShouldConflict()
    {
        var team = ExistingTeam("Finanzas", "FIN");
        _teams.ExistsByNameAsync("Tecnología", team.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new UpdateTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(new UpdateTeamCommand(team.Id, "Tecnología", "FIN", null, true), CancellationToken.None);

        result.Error.Code.ShouldBe("Team.NameAlreadyExists");
        team.Name.ShouldBe("Finanzas");
    }

    // ---------- Patch ----------

    [Fact]
    public async Task Patch_ShouldOnlyChangeProvidedFields()
    {
        var team = ExistingTeam("Finanzas", "FIN", "Ana");
        var handler = new PatchTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(new PatchTeamCommand(team.Id, null, null, null, IsActive: false), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        team.Name.ShouldBe("Finanzas");
        team.Code.ShouldBe("FIN");
        team.Coach.ShouldBe("Ana");
        team.IsActive.ShouldBeFalse();
        await _teams.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Patch_WithEmptyCoach_ShouldClearIt()
    {
        var team = ExistingTeam("Finanzas", "FIN", "Ana");
        var handler = new PatchTeamCommandHandler(_teams, _unitOfWork);

        await handler.Handle(new PatchTeamCommand(team.Id, null, null, "", null), CancellationToken.None);

        team.Coach.ShouldBeNull();
    }

    [Fact]
    public async Task Patch_WithTakenCode_ShouldConflict()
    {
        var team = ExistingTeam("Finanzas", "FIN");
        _teams.ExistsByCodeAsync("TEC", team.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new PatchTeamCommandHandler(_teams, _unitOfWork);

        var result = await handler.Handle(new PatchTeamCommand(team.Id, null, "tec", null, null), CancellationToken.None);

        result.Error.Code.ShouldBe("Team.CodeAlreadyExists");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_WhenMissing_ShouldSucceedWithoutCommit()
    {
        _teams.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Team?)null);
        var handler = new DeleteTeamCommandHandler(_teams, _matches, _unitOfWork);

        var result = await handler.Handle(new DeleteTeamCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WithMatches_ShouldConflict()
    {
        var team = ExistingTeam("Finanzas", "FIN");
        _matches.AnyInvolvingTeamAsync(team.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new DeleteTeamCommandHandler(_teams, _matches, _unitOfWork);

        var result = await handler.Handle(new DeleteTeamCommand(team.Id), CancellationToken.None);

        result.Error.ShouldBe(TeamErrors.HasMatches);
        _teams.DidNotReceive().Remove(Arg.Any<Team>());
    }

    [Fact]
    public async Task Delete_WithoutMatches_ShouldRemoveAndCommit()
    {
        var team = ExistingTeam("Finanzas", "FIN");
        var handler = new DeleteTeamCommandHandler(_teams, _matches, _unitOfWork);

        var result = await handler.Handle(new DeleteTeamCommand(team.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _teams.Received(1).Remove(team);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private Team ExistingTeam(string name, string code, string? coach = null)
    {
        var team = Team.Create(name, code, coach).Value;
        team.ClearDomainEvents();
        _teams.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        return team;
    }
}
