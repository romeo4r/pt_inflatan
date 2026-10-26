using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure.Persistence;

namespace Mundialito.Infrastructure.Seeding;

/// <summary>
/// Fills an empty database with a small, realistic tournament: 4 teams, 5 players each,
/// 6 fixtures (full round robin) of which 3 are finished with goals. Everything goes through
/// the domain factories and the Unit of Work, so invariants, audit rows and domain events behave
/// exactly as they do for API requests. One transaction: the seed is all or nothing.
/// </summary>
internal sealed partial class DatabaseSeeder(
    ApplicationDbContext context,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<DatabaseSeeder> logger)
{
    public const int TeamCount = 4;
    public const int PlayersPerTeam = 5;
    public const int MatchCount = 6;
    public const int FinishedMatchCount = 3;

    private static readonly (string Name, string Code, string Coach)[] TeamSeeds =
    [
        ("Finanzas FC", "FIN", "Ana Pérez"),
        ("Tecnología City", "TEC", "Luis Mejía"),
        ("Logística Athletic", "LOG", "Carmen Díaz"),
        ("Recursos Humanos United", "RRHH", "Jorge Castro"),
    ];

    private static readonly (int Jersey, PlayerPosition Position)[] RosterTemplate =
    [
        (1, PlayerPosition.Goalkeeper),
        (4, PlayerPosition.Defender),
        (8, PlayerPosition.Midfielder),
        (10, PlayerPosition.Midfielder),
        (9, PlayerPosition.Forward),
    ];

    private static readonly string[][] PlayerNames =
    [
        ["Mario Rivas", "Diego Soto", "Pablo Reyes", "Óscar Molina", "Carlos Gómez"],
        ["Iván Torres", "Raúl Campos", "Julio Herrera", "Andrés Peña", "Marco Flores"],
        ["Hugo Navarro", "Samuel Ortiz", "Daniel Vega", "Elías Ramírez", "Bruno Chávez"],
        ["Tomás Aguilar", "Felipe Cruz", "Gabriel Lara", "Rodrigo Mena", "Sergio Ayala"],
    ];

    /// <summary>Returns true when data was inserted, false when the database already had teams.</summary>
    public async Task<bool> SeedAsync(CancellationToken cancellationToken)
    {
        if (await context.Teams.AnyAsync(cancellationToken))
        {
            LogSkipped(logger);
            return false;
        }

        var teams = CreateTeams();
        var players = CreatePlayers(teams);
        var matches = CreateMatches(teams);
        RegisterResults(teams, players, matches);

        context.Teams.AddRange(teams);
        context.Players.AddRange(players.SelectMany(p => p.Value));
        context.Matches.AddRange(matches);

        var commit = await unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            throw new InvalidOperationException($"Database seed failed: {commit.Error.Code} - {commit.Error.Message}");
        }

        LogSeeded(logger, teams.Count, players.Sum(p => p.Value.Count), matches.Count, FinishedMatchCount);
        return true;
    }

    private static List<Team> CreateTeams() =>
        TeamSeeds.Select(t => Team.Create(t.Name, t.Code, t.Coach).Value).ToList();

    private Dictionary<string, List<Player>> CreatePlayers(List<Team> teams)
    {
        var today = clock.Today;
        var rosters = new Dictionary<string, List<Player>>(StringComparer.Ordinal);

        for (var t = 0; t < teams.Count; t++)
        {
            var roster = new List<Player>();
            for (var i = 0; i < RosterTemplate.Length; i++)
            {
                var (firstName, lastName) = SplitName(PlayerNames[t][i]);
                var birthDate = today.AddYears(-24 - i).AddMonths(-t);

                roster.Add(Player.Create(
                    teams[t].Id, firstName, lastName, RosterTemplate[i].Jersey, RosterTemplate[i].Position, birthDate, today).Value);
            }

            rosters[teams[t].Code] = roster;
        }

        return rosters;
    }

    /// <summary>
    /// Full round robin for 4 teams: 3 match days, 2 matches each. The three finished matches are
    /// in the past; the three pending ones (one postponed from day 2, plus day 3) are in the future.
    /// </summary>
    private List<Match> CreateMatches(List<Team> teams)
    {
        var fin = teams[0];
        var tec = teams[1];
        var log = teams[2];
        var rrhh = teams[3];
        var baseDate = clock.UtcNow.Date.AddHours(15);

        return
        [
            Match.Schedule(fin.Id, tec.Id, 1, baseDate.AddDays(-14), "Cancha Principal").Value,
            Match.Schedule(log.Id, rrhh.Id, 1, baseDate.AddDays(-14).AddHours(2), "Cancha Principal").Value,
            Match.Schedule(fin.Id, rrhh.Id, 2, baseDate.AddDays(-7), "Cancha 2").Value,
            // Match day 2 fixture that was postponed: still pending, played after the original date.
            Match.Schedule(tec.Id, log.Id, 2, baseDate.AddDays(3), "Cancha 2").Value,
            Match.Schedule(fin.Id, log.Id, 3, baseDate.AddDays(7), "Cancha Principal").Value,
            Match.Schedule(rrhh.Id, tec.Id, 3, baseDate.AddDays(7).AddHours(2), "Cancha Principal").Value,
        ];
    }

    /// <summary>Three results so the standings and the top scorers have something to show.</summary>
    private void RegisterResults(List<Team> teams, Dictionary<string, List<Player>> players, List<Match> matches)
    {
        var fin = teams[0];
        var tec = teams[1];
        var rrhh = teams[3];

        Player Forward(string code) => players[code][4];
        Player Midfielder(string code) => players[code][3];

        // Match day 1: FIN 2-1 TEC
        Ensure(matches[0].RegisterResult(2, 1,
        [
            new GoalEntry(Forward("FIN").Id, fin.Id, 12),
            new GoalEntry(Midfielder("FIN").Id, fin.Id, 58),
            new GoalEntry(Forward("TEC").Id, tec.Id, 74),
        ], matches[0].ScheduledAtUtc.AddHours(2)));

        // Match day 1: LOG 0-0 RRHH
        Ensure(matches[1].RegisterResult(0, 0, [], matches[1].ScheduledAtUtc.AddHours(2)));

        // Match day 2: FIN 1-3 RRHH
        Ensure(matches[2].RegisterResult(1, 3,
        [
            new GoalEntry(Forward("FIN").Id, fin.Id, 33),
            new GoalEntry(Forward("RRHH").Id, rrhh.Id, 15),
            new GoalEntry(Forward("RRHH").Id, rrhh.Id, 61),
            new GoalEntry(Midfielder("RRHH").Id, rrhh.Id, 80),
        ], matches[2].ScheduledAtUtc.AddHours(2)));
    }

    private static void Ensure(Domain.Common.Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seed data violates a domain rule: {result.Error.Code}");
        }
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var separator = fullName.IndexOf(' ', StringComparison.Ordinal);
        return (fullName[..separator], fullName[(separator + 1)..]);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Database already contains teams; seed skipped")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Database seeded with {Teams} teams, {Players} players, {Matches} matches ({Finished} finished)")]
    private static partial void LogSeeded(ILogger logger, int teams, int players, int matches, int finished);
}
