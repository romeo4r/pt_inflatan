using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mundialito.Application;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Tournament;
using Mundialito.Infrastructure.Events;
using Mundialito.Infrastructure.Persistence;
using Mundialito.Infrastructure.Persistence.DomainEvents;
using Mundialito.Infrastructure.Seeding;
using NSubstitute;

namespace Mundialito.UnitTests.Infrastructure.Seeding;

/// <summary>Runs the real seeder through the real Unit of Work, dispatcher and event handlers on SQLite.</summary>
public sealed class DatabaseSeederTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private readonly ApplicationDbContext _context;
    private readonly ServiceProvider _provider;
    private readonly DatabaseSeeder _seeder;

    public DatabaseSeederTests()
    {
        _context = _factory.CreateContext();

        var requestContext = Substitute.For<IRequestContext>();
        requestContext.CorrelationId.Returns((string?)null);
        requestContext.TraceId.Returns((string?)null);

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IClock>(new FixedClock(Now));
        services.AddSingleton(requestContext);
        services.AddSingleton(_context);
        services.AddScoped<IDomainEventLogRepository, DomainEventLogRepository>();
        services.AddApplication();
        _provider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(_provider, NullLogger<DomainEventDispatcher>.Instance);
        var unitOfWork = new UnitOfWork(_context, dispatcher, new FixedClock(Now), NullLogger<UnitOfWork>.Instance);
        _seeder = new DatabaseSeeder(_context, unitOfWork, new FixedClock(Now), NullLogger<DatabaseSeeder>.Instance);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _context.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Seed_OnEmptyDatabase_ShouldInsertTheRequiredDataSet()
    {
        var seeded = await _seeder.SeedAsync(CancellationToken.None);

        seeded.ShouldBeTrue();
        await using var verification = _factory.CreateContext();
        (await verification.Teams.CountAsync()).ShouldBe(DatabaseSeeder.TeamCount);
        (await verification.Players.CountAsync()).ShouldBe(DatabaseSeeder.TeamCount * DatabaseSeeder.PlayersPerTeam);
        (await verification.Matches.CountAsync()).ShouldBe(DatabaseSeeder.MatchCount);
        (await verification.Matches.CountAsync(m => m.Status == MatchStatus.Finished)).ShouldBe(DatabaseSeeder.FinishedMatchCount);
        (await verification.Matches.CountAsync(m => m.Status == MatchStatus.Scheduled)).ShouldBe(DatabaseSeeder.MatchCount - DatabaseSeeder.FinishedMatchCount);
        (await verification.Goals.CountAsync()).ShouldBe(7);
    }

    [Fact]
    public async Task Seed_ShouldGiveEveryTeamFivePlayersWithUniqueJerseys()
    {
        await _seeder.SeedAsync(CancellationToken.None);

        await using var verification = _factory.CreateContext();
        var rosters = await verification.Players.GroupBy(p => p.TeamId).Select(g => g.Count()).ToListAsync();
        rosters.ShouldAllBe(count => count == DatabaseSeeder.PlayersPerTeam);
        rosters.Count.ShouldBe(DatabaseSeeder.TeamCount);
    }

    [Fact]
    public async Task Seed_ShouldProduceAMeaningfulStandingsTable()
    {
        await _seeder.SeedAsync(CancellationToken.None);

        await using var verification = _factory.CreateContext();
        var teams = await verification.Teams.Select(t => new TournamentTeam(t.Id, t.Name, t.Code)).ToListAsync();
        var played = await verification.Matches
            .Where(m => m.Status == MatchStatus.Finished)
            .Select(m => new PlayedMatch(m.HomeTeamId, m.AwayTeamId, m.HomeScore!.Value, m.AwayScore!.Value))
            .ToListAsync();

        var rows = StandingsCalculator.Calculate(teams, played);

        // FIN 2-1 TEC, LOG 0-0 RRHH, FIN 1-3 RRHH -> RRHH 4, FIN 3, LOG 1, TEC 0
        rows.Select(r => r.TeamCode).ShouldBe(["RRHH", "FIN", "LOG", "TEC"]);
        rows.Select(r => r.Points).ShouldBe([4, 3, 1, 0]);
    }

    [Fact]
    public async Task Seed_ShouldRecordDomainEventsForTeamsAndResults()
    {
        await _seeder.SeedAsync(CancellationToken.None);

        await using var verification = _factory.CreateContext();
        (await verification.DomainEventLogs.CountAsync(e => e.EventType == "TeamCreatedDomainEvent")).ShouldBe(4);
        (await verification.DomainEventLogs.CountAsync(e => e.EventType == "MatchResultRegisteredDomainEvent")).ShouldBe(3);
    }

    [Fact]
    public async Task Seed_WhenDataExists_ShouldDoNothing()
    {
        await _seeder.SeedAsync(CancellationToken.None);

        var secondRun = await _seeder.SeedAsync(CancellationToken.None);

        secondRun.ShouldBeFalse();
        await using var verification = _factory.CreateContext();
        (await verification.Teams.CountAsync()).ShouldBe(DatabaseSeeder.TeamCount);
    }

    [Fact]
    public async Task Seed_ShouldPlaceFinishedMatchesInThePastAndScheduledOnesInTheFuture()
    {
        await _seeder.SeedAsync(CancellationToken.None);

        await using var verification = _factory.CreateContext();
        var matches = await verification.Matches.ToListAsync();
        matches.Where(m => m.Status == MatchStatus.Finished).ShouldAllBe(m => m.ScheduledAtUtc < Now);
        matches.Where(m => m.Status == MatchStatus.Scheduled).ShouldAllBe(m => m.ScheduledAtUtc > Now);
    }
}
