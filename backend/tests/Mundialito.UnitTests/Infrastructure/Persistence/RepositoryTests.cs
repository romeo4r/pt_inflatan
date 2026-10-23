using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure.Persistence.Repositories;

namespace Mundialito.UnitTests.Infrastructure.Persistence;

public sealed class RepositoryTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TeamRepository_ExistsByCode_ShouldNormalizeAndHonourExclusion()
    {
        await using var context = _factory.CreateContext();
        var repository = new TeamRepository(context);
        var team = Team.Create("Finanzas", "FIN", null).Value;
        repository.Add(team);
        await context.SaveChangesAsync();

        (await repository.ExistsByCodeAsync(" fin ")).ShouldBeTrue();
        (await repository.ExistsByCodeAsync("FIN", excludeId: team.Id)).ShouldBeFalse();
        (await repository.ExistsByNameAsync("Finanzas")).ShouldBeTrue();
        (await repository.ExistsByNameAsync("Finanzas", excludeId: team.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task PlayerRepository_ShouldLoadByIdsAndDetectJerseyConflicts()
    {
        await using var context = _factory.CreateContext();
        var team = Team.Create("Finanzas", "FIN", null).Value;
        context.Teams.Add(team);
        var teamId = team.Id;

        var repository = new PlayerRepository(context);
        var first = Player.Create(teamId, "Carlos", "Gómez", 9, PlayerPosition.Forward, null, Today).Value;
        var second = Player.Create(teamId, "Luis", "Pérez", 1, PlayerPosition.Goalkeeper, null, Today).Value;
        repository.Add(first);
        repository.Add(second);
        await context.SaveChangesAsync();

        var loaded = await repository.GetByIdsAsync([first.Id, second.Id, Guid.CreateVersion7()]);
        loaded.Count.ShouldBe(2);

        (await repository.GetByIdsAsync([])).ShouldBeEmpty();
        (await repository.ExistsByJerseyNumberAsync(teamId, 9)).ShouldBeTrue();
        (await repository.ExistsByJerseyNumberAsync(teamId, 9, excludeId: first.Id)).ShouldBeFalse();
        (await repository.ExistsByJerseyNumberAsync(teamId, 10)).ShouldBeFalse();
    }

    [Fact]
    public async Task PlayerRepository_HasGoals_ShouldReflectRegisteredResults()
    {
        await using var context = _factory.CreateContext();
        var home = Team.Create("Finanzas", "FIN", null).Value;
        var away = Team.Create("Tecnología", "TEC", null).Value;
        var scorer = Player.Create(home.Id, "Carlos", "Gómez", 9, PlayerPosition.Forward, null, Today).Value;
        var benched = Player.Create(home.Id, "Luis", "Pérez", 1, PlayerPosition.Goalkeeper, null, Today).Value;
        var match = Match.Schedule(home.Id, away.Id, 1, Now, null).Value;
        match.RegisterResult(1, 0, [new GoalEntry(scorer.Id, home.Id, 10)], Now.AddHours(2));
        context.AddRange(home, away, scorer, benched, match);
        await context.SaveChangesAsync();
        var repository = new PlayerRepository(context);

        (await repository.HasGoalsAsync(scorer.Id)).ShouldBeTrue();
        (await repository.HasGoalsAsync(benched.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task MatchRepository_AnyInvolvingTeam_ShouldDetectHomeAndAway()
    {
        await using var context = _factory.CreateContext();
        var home = Team.Create("Finanzas", "FIN", null).Value;
        var away = Team.Create("Tecnología", "TEC", null).Value;
        var idle = Team.Create("Logística", "LOG", null).Value;
        context.Teams.AddRange(home, away, idle);
        var repository = new MatchRepository(context);
        repository.Add(Match.Schedule(home.Id, away.Id, 1, Now, null).Value);
        await context.SaveChangesAsync();

        (await repository.AnyInvolvingTeamAsync(home.Id)).ShouldBeTrue();
        (await repository.AnyInvolvingTeamAsync(away.Id)).ShouldBeTrue();
        (await repository.AnyInvolvingTeamAsync(idle.Id)).ShouldBeFalse();
    }
}
