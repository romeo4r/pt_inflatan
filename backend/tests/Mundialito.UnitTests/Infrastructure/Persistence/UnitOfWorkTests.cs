using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Matches.Events;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;
using Mundialito.Domain.Teams.Events;
using Mundialito.Infrastructure.Persistence;
using Mundialito.Infrastructure.Persistence.Repositories;
using NSubstitute;

namespace Mundialito.UnitTests.Infrastructure.Persistence;

public sealed class UnitOfWorkTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly FixedClock _clock = new(Now);

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CommitAsync_ShouldPersistChangesFromSeveralRepositoriesAtomically()
    {
        await using var context = _factory.CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var teams = new TeamRepository(context);
        var players = new PlayerRepository(context);

        var team = Team.Create("Finanzas", "FIN", null).Value;
        var player = Player.Create(team.Id, "Carlos", "Gómez", 9, PlayerPosition.Forward, null, DateOnly.FromDateTime(Now)).Value;
        teams.Add(team);
        players.Add(player);

        var affected = await unitOfWork.CommitAsync();

        affected.ShouldBe(2);
        await using var verification = _factory.CreateContext();
        (await verification.Teams.CountAsync()).ShouldBe(1);
        (await verification.Players.SingleAsync()).TeamId.ShouldBe(team.Id);
    }

    [Fact]
    public async Task Add_WithoutCommit_ShouldNotReachDatabase()
    {
        await using var context = _factory.CreateContext();
        new TeamRepository(context).Add(Team.Create("Finanzas", "FIN", null).Value);

        await using var verification = _factory.CreateContext();
        (await verification.Teams.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task CommitAsync_ShouldStampCreatedAndUpdatedTimestamps()
    {
        await using var context = _factory.CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var team = Team.Create("Finanzas", "FIN", null).Value;
        context.Teams.Add(team);

        await unitOfWork.CommitAsync();

        team.CreatedAtUtc.ShouldBe(Now);
        team.UpdatedAtUtc.ShouldBeNull();

        _clock.UtcNow = Now.AddHours(1);
        team.Update("Finanzas FC", "FIN", null);
        await unitOfWork.CommitAsync();

        team.CreatedAtUtc.ShouldBe(Now);
        team.UpdatedAtUtc.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public async Task CommitAsync_ShouldDispatchDomainEventsOnceAndClearThem()
    {
        await using var context = _factory.CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var team = Team.Create("Finanzas", "FIN", null).Value;
        context.Teams.Add(team);

        await unitOfWork.CommitAsync();
        await unitOfWork.CommitAsync();

        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyCollection<IDomainEvent>>(events =>
                events.Count == 1 && events.Single() is TeamCreatedDomainEvent),
            Arg.Any<CancellationToken>());
        team.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task CommitAsync_WhenDispatcherFails_ShouldRollBackEverything()
    {
        await using var context = _factory.CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        context.Teams.Add(Team.Create("Finanzas", "FIN", null).Value);
        _dispatcher
            .DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("handler exploded"));

        await Should.ThrowAsync<InvalidOperationException>(() => unitOfWork.CommitAsync());

        await using var verification = _factory.CreateContext();
        (await verification.Teams.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task CommitAsync_ShouldPersistRowsAddedByEventHandlersInSameTransaction()
    {
        await using var context = _factory.CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        context.Teams.Add(Team.Create("Finanzas", "FIN", null).Value);

        // Simulates a handler that reacts to the first TeamCreated by persisting another team.
        // That second team raises its own event, which exercises the multi-round dispatch.
        var reacted = false;
        _dispatcher
            .When(d => d.DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                if (!reacted)
                {
                    reacted = true;
                    context.Teams.Add(Team.Create("Reacción", "REA", null).Value);
                }
            });

        var affected = await unitOfWork.CommitAsync();

        affected.ShouldBe(2);
        await _dispatcher.Received(2).DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>());
        await using var verification = _factory.CreateContext();
        (await verification.Teams.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task CommitAsync_WhenUniqueIndexIsViolated_ShouldThrowAndPersistNothing()
    {
        await using var context = _factory.CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        context.Teams.Add(Team.Create("Finanzas", "FIN", null).Value);
        await unitOfWork.CommitAsync();

        context.Teams.Add(Team.Create("Otro", "FIN", null).Value);

        await Should.ThrowAsync<DbUpdateException>(() => unitOfWork.CommitAsync());
    }

    [Fact]
    public async Task MatchAggregate_ShouldRoundTripWithGoalsAndRaiseResultEvent()
    {
        await using var context = _factory.CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var home = Team.Create("Finanzas", "FIN", null).Value;
        var away = Team.Create("Tecnología", "TEC", null).Value;
        var scorer = Player.Create(home.Id, "Carlos", "Gómez", 9, PlayerPosition.Forward, null, DateOnly.FromDateTime(Now)).Value;
        var match = Match.Schedule(home.Id, away.Id, 1, Now, "Cancha 1").Value;
        context.AddRange(home, away, scorer, match);
        await unitOfWork.CommitAsync();
        _dispatcher.ClearReceivedCalls();

        match.RegisterResult(1, 0, [new GoalEntry(scorer.Id, home.Id, 33)], Now.AddHours(2)).IsSuccess.ShouldBeTrue();
        await unitOfWork.CommitAsync();

        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyCollection<IDomainEvent>>(events => events.Single() is MatchResultRegisteredDomainEvent),
            Arg.Any<CancellationToken>());

        await using var verification = _factory.CreateContext();
        var stored = await new MatchRepository(verification).GetByIdAsync(match.Id);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(MatchStatus.Finished);
        stored.HomeScore.ShouldBe(1);
        stored.Goals.ShouldHaveSingleItem().PlayerId.ShouldBe(scorer.Id);
    }

    [Fact]
    public async Task Schema_ShouldRejectMatchWithSameTeamOnBothSides()
    {
        await using var context = _factory.CreateContext();
        var team = Team.Create("Finanzas", "FIN", null).Value;
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        // The domain blocks this, so bypass it with raw SQL to prove the database check constraint exists.
        // Raw SQL surfaces the provider exception directly (DbException), not DbUpdateException.
        await Should.ThrowAsync<DbException>(() => context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Matches (Id, HomeTeamId, AwayTeamId, MatchDay, ScheduledAtUtc, Status, CreatedAtUtc)
            VALUES ({Guid.CreateVersion7()}, {team.Id}, {team.Id}, 1, {Now}, 1, {Now})
            """));
    }

    private UnitOfWork CreateUnitOfWork(ApplicationDbContext context) =>
        new(context, _dispatcher, _clock, NullLogger<UnitOfWork>.Instance);
}
