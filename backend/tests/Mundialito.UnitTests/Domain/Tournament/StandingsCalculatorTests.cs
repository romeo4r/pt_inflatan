using Mundialito.Domain.Tournament;

namespace Mundialito.UnitTests.Domain.Tournament;

public class StandingsCalculatorTests
{
    private static readonly TournamentTeam Fin = new(Guid.CreateVersion7(), "Finanzas", "FIN");
    private static readonly TournamentTeam Tec = new(Guid.CreateVersion7(), "Tecnología", "TEC");
    private static readonly TournamentTeam Log = new(Guid.CreateVersion7(), "Logística", "LOG");
    private static readonly TournamentTeam Rrh = new(Guid.CreateVersion7(), "Recursos Humanos", "RRHH");
    private static readonly TournamentTeam[] Teams = [Fin, Tec, Log, Rrh];

    [Fact]
    public void Calculate_WithoutMatches_ShouldListEveryTeamAtZeroOrderedByName()
    {
        var rows = StandingsCalculator.Calculate(Teams, []);

        rows.Count.ShouldBe(4);
        rows.Select(r => r.TeamCode).ShouldBe(["FIN", "LOG", "RRHH", "TEC"]);
        rows.ShouldAllBe(r => r.Played == 0 && r.Points == 0 && r.GoalDifference == 0);
        rows.Select(r => r.Position).ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void Calculate_ShouldAwardThreeForWinOneForDrawZeroForLoss()
    {
        var matches = new[]
        {
            new PlayedMatch(Fin.Id, Tec.Id, 2, 0), // FIN wins
            new PlayedMatch(Log.Id, Rrh.Id, 1, 1), // draw
        };

        var rows = StandingsCalculator.Calculate(Teams, matches).ToDictionary(r => r.TeamCode);

        rows["FIN"].Points.ShouldBe(3);
        rows["FIN"].Won.ShouldBe(1);
        rows["TEC"].Points.ShouldBe(0);
        rows["TEC"].Lost.ShouldBe(1);
        rows["LOG"].Points.ShouldBe(1);
        rows["LOG"].Drawn.ShouldBe(1);
        rows["RRHH"].Points.ShouldBe(1);
        rows.Values.ShouldAllBe(r => r.Played == 1);
    }

    [Fact]
    public void Calculate_ShouldAccumulateGoalsForAgainstAndDifference()
    {
        var matches = new[]
        {
            new PlayedMatch(Fin.Id, Tec.Id, 3, 1),
            new PlayedMatch(Log.Id, Fin.Id, 2, 2),
        };

        var fin = StandingsCalculator.Calculate(Teams, matches).Single(r => r.TeamCode == "FIN");

        fin.Played.ShouldBe(2);
        fin.GoalsFor.ShouldBe(5);
        fin.GoalsAgainst.ShouldBe(3);
        fin.GoalDifference.ShouldBe(2);
        fin.Points.ShouldBe(4);
    }

    [Fact]
    public void Calculate_ShouldOrderByPointsThenGoalDifferenceThenGoalsFor()
    {
        var matches = new[]
        {
            new PlayedMatch(Fin.Id, Rrh.Id, 1, 0), // FIN 3 pts, GD +1, GF 1
            new PlayedMatch(Tec.Id, Rrh.Id, 3, 1), // TEC 3 pts, GD +2, GF 3
            new PlayedMatch(Log.Id, Rrh.Id, 2, 1), // LOG 3 pts, GD +1, GF 2
        };

        var codes = StandingsCalculator.Calculate(Teams, matches).Select(r => r.TeamCode).ToArray();

        // TEC by goal difference; LOG over FIN by goals for (same points, same difference); RRHH last.
        codes.ShouldBe(["TEC", "LOG", "FIN", "RRHH"]);
    }

    [Fact]
    public void Calculate_WhenFullyTied_ShouldFallBackToTeamName()
    {
        var matches = new[]
        {
            new PlayedMatch(Fin.Id, Tec.Id, 1, 1),
            new PlayedMatch(Log.Id, Rrh.Id, 1, 1),
        };

        var codes = StandingsCalculator.Calculate(Teams, matches).Select(r => r.TeamCode).ToArray();

        codes.ShouldBe(["FIN", "LOG", "RRHH", "TEC"]);
    }

    [Fact]
    public void Calculate_ShouldAssignSequentialPositions()
    {
        var matches = new[] { new PlayedMatch(Tec.Id, Fin.Id, 4, 0) };

        var rows = StandingsCalculator.Calculate(Teams, matches);

        rows.Select(r => r.Position).ShouldBe([1, 2, 3, 4]);
        rows[0].TeamCode.ShouldBe("TEC");
        rows[3].TeamCode.ShouldBe("FIN");
    }

    [Fact]
    public void Calculate_ShouldIgnoreMatchesOfUnknownTeams()
    {
        var stranger = Guid.CreateVersion7();
        var matches = new[]
        {
            new PlayedMatch(Fin.Id, stranger, 5, 0),
            new PlayedMatch(Fin.Id, Tec.Id, 1, 0),
        };

        var fin = StandingsCalculator.Calculate(Teams, matches).Single(r => r.TeamCode == "FIN");

        fin.Played.ShouldBe(1);
        fin.GoalsFor.ShouldBe(1);
        fin.Points.ShouldBe(3);
    }

    [Fact]
    public void Calculate_FullRoundRobin_ShouldBeConsistent()
    {
        var matches = new[]
        {
            new PlayedMatch(Fin.Id, Tec.Id, 2, 1),
            new PlayedMatch(Log.Id, Rrh.Id, 0, 0),
            new PlayedMatch(Fin.Id, Log.Id, 1, 1),
            new PlayedMatch(Tec.Id, Rrh.Id, 3, 0),
            new PlayedMatch(Fin.Id, Rrh.Id, 0, 1),
            new PlayedMatch(Tec.Id, Log.Id, 2, 2),
        };

        var rows = StandingsCalculator.Calculate(Teams, matches);

        rows.Sum(r => r.GoalsFor).ShouldBe(rows.Sum(r => r.GoalsAgainst));
        rows.Sum(r => r.Won).ShouldBe(rows.Sum(r => r.Lost));
        rows.Sum(r => r.Played).ShouldBe(matches.Length * 2);
        // TEC 4 pts (+2), FIN 4 pts (0), RRHH 4 pts (-2), LOG 3 pts.
        rows.Select(r => r.TeamCode).ShouldBe(["TEC", "FIN", "RRHH", "LOG"]);
        rows.Select(r => r.Points).ShouldBe([4, 4, 4, 3]);
        rows.Select(r => r.GoalDifference).ShouldBe([2, 0, -2, 0]);
    }
}
