using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mundialito.Application;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Matches.Events;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;
using Mundialito.Domain.Teams.Events;
using Mundialito.Infrastructure.Events;
using Mundialito.Infrastructure.Persistence;
using Mundialito.Infrastructure.Persistence.DomainEvents;
using NSubstitute;

namespace Mundialito.UnitTests.Infrastructure.Persistence;

/// <summary>
/// End-to-end over the real pieces: Unit of Work + dispatcher + the production event handlers
/// + the EF repository, on SQLite. Proves the audit row lands in the same commit as the change.
/// </summary>
public sealed class DomainEventTraceabilityTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private readonly ApplicationDbContext _context;
    private readonly ServiceProvider _provider;
    private readonly UnitOfWork _unitOfWork;

    public DomainEventTraceabilityTests()
    {
        _context = _factory.CreateContext();

        var requestContext = Substitute.For<IRequestContext>();
        requestContext.CorrelationId.Returns("corr-e2e");
        requestContext.TraceId.Returns("trace-e2e");

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IClock>(new FixedClock(Now));
        services.AddSingleton(requestContext);
        services.AddSingleton(_context);
        services.AddScoped<IDomainEventLogRepository, DomainEventLogRepository>();
        services.AddApplication(); // registers the real IDomainEventHandler implementations
        _provider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(_provider, NullLogger<DomainEventDispatcher>.Instance);
        _unitOfWork = new UnitOfWork(_context, dispatcher, new FixedClock(Now), NullLogger<UnitOfWork>.Instance);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _context.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task CreatingATeam_ShouldPersistItsEventInTheSameCommit()
    {
        var team = Team.Create("Finanzas", "FIN", null).Value;
        _context.Teams.Add(team);

        var commit = await _unitOfWork.CommitAsync();

        commit.IsSuccess.ShouldBeTrue();
        commit.Value.ShouldBe(2); // the team + its audit row
        await using var verification = _factory.CreateContext();
        var entry = await verification.DomainEventLogs.SingleAsync();
        entry.EventType.ShouldBe(nameof(TeamCreatedDomainEvent));
        entry.AggregateId.ShouldBe(team.Id);
        entry.CorrelationId.ShouldBe("corr-e2e");
        entry.TraceId.ShouldBe("trace-e2e");
        entry.RecordedAtUtc.ShouldBe(Now);
        entry.Payload.ShouldContain("\"code\":\"FIN\"");
    }

    [Fact]
    public async Task RegisteringAResult_ShouldPersistItsEventAlongsideScoreAndGoals()
    {
        var home = Team.Create("Finanzas", "FIN", null).Value;
        var away = Team.Create("Tecnología", "TEC", null).Value;
        var scorer = Player.Create(home.Id, "Carlos", "Gómez", 9, PlayerPosition.Forward, null, DateOnly.FromDateTime(Now)).Value;
        var match = Match.Schedule(home.Id, away.Id, 1, Now, null).Value;
        _context.AddRange(home, away, scorer, match);
        await _unitOfWork.CommitAsync();

        match.RegisterResult(1, 0, [new GoalEntry(scorer.Id, home.Id, 20)], Now.AddHours(2));
        var commit = await _unitOfWork.CommitAsync();

        commit.IsSuccess.ShouldBeTrue();
        await using var verification = _factory.CreateContext();
        var events = await verification.DomainEventLogs.OrderBy(e => e.RecordedAtUtc).ToListAsync();
        events.Count.ShouldBe(3); // two TeamCreated + one MatchResultRegistered
        var resultEvent = events.Single(e => e.EventType == nameof(MatchResultRegisteredDomainEvent));
        resultEvent.AggregateId.ShouldBe(match.Id);
        resultEvent.Payload.ShouldContain("\"homeScore\":1");
        (await verification.Goals.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task WhenTheCommitFails_NoEventRowShouldSurvive()
    {
        _context.Teams.Add(Team.Create("Finanzas", "FIN", null).Value);
        await _unitOfWork.CommitAsync();

        // Duplicate code violates the unique index: the second team and its event must both vanish.
        _context.Teams.Add(Team.Create("Otro", "FIN", null).Value);
        var commit = await _unitOfWork.CommitAsync();

        commit.IsFailure.ShouldBeTrue();
        await using var verification = _factory.CreateContext();
        (await verification.DomainEventLogs.CountAsync()).ShouldBe(1);
        (await verification.Teams.CountAsync()).ShouldBe(1);
    }
}
