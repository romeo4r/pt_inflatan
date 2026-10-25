namespace Mundialito.Domain.Tournament;

/// <summary>A team as the standings need it: identity and display data only.</summary>
public sealed record TournamentTeam(Guid Id, string Name, string Code);

/// <summary>A finished match reduced to what the table needs.</summary>
public sealed record PlayedMatch(Guid HomeTeamId, Guid AwayTeamId, int HomeScore, int AwayScore);

/// <summary>One line of the standings. <see cref="Position"/> is 1-based after ordering.</summary>
public sealed record StandingRow(
    int Position,
    Guid TeamId,
    string TeamName,
    string TeamCode,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points);

/// <summary>
/// Pure tournament arithmetic: no I/O, no clock, deterministic. The read side fetches teams and
/// finished matches and hands them here, so the ranking rules live in one tested place.
/// Order: points, then goal difference, then goals for, then team name (stable tiebreaker).
/// </summary>
public static class StandingsCalculator
{
    public const int PointsForWin = 3;
    public const int PointsForDraw = 1;
    public const int PointsForLoss = 0;

    public static IReadOnlyList<StandingRow> Calculate(
        IEnumerable<TournamentTeam> teams,
        IEnumerable<PlayedMatch> matches)
    {
        var tallies = teams.ToDictionary(t => t.Id, t => new Tally(t));

        foreach (var match in matches)
        {
            // Matches referencing teams outside the given set are ignored rather than failing:
            // the table is a projection and must never break because of a stray row.
            if (!tallies.TryGetValue(match.HomeTeamId, out var home) || !tallies.TryGetValue(match.AwayTeamId, out var away))
            {
                continue;
            }

            home.Record(scored: match.HomeScore, conceded: match.AwayScore);
            away.Record(scored: match.AwayScore, conceded: match.HomeScore);
        }

        return tallies.Values
            .OrderByDescending(t => t.Points)
            .ThenByDescending(t => t.GoalDifference)
            .ThenByDescending(t => t.GoalsFor)
            .ThenBy(t => t.Team.Name, StringComparer.OrdinalIgnoreCase)
            .Select((t, index) => t.ToRow(position: index + 1))
            .ToList();
    }

    private sealed class Tally(TournamentTeam team)
    {
        public TournamentTeam Team { get; } = team;

        public int Played { get; private set; }

        public int Won { get; private set; }

        public int Drawn { get; private set; }

        public int Lost { get; private set; }

        public int GoalsFor { get; private set; }

        public int GoalsAgainst { get; private set; }

        public int GoalDifference => GoalsFor - GoalsAgainst;

        public int Points => (Won * PointsForWin) + (Drawn * PointsForDraw) + (Lost * PointsForLoss);

        public void Record(int scored, int conceded)
        {
            Played++;
            GoalsFor += scored;
            GoalsAgainst += conceded;

            if (scored > conceded)
            {
                Won++;
            }
            else if (scored == conceded)
            {
                Drawn++;
            }
            else
            {
                Lost++;
            }
        }

        public StandingRow ToRow(int position) => new(
            position, Team.Id, Team.Name, Team.Code,
            Played, Won, Drawn, Lost, GoalsFor, GoalsAgainst, GoalDifference, Points);
    }
}
