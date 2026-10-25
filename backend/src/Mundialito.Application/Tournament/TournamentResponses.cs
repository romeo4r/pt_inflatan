namespace Mundialito.Application.Tournament;

public sealed record StandingRowResponse(
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

/// <summary>The whole table plus how many finished matches it is built from.</summary>
public sealed record StandingsResponse(IReadOnlyList<StandingRowResponse> Rows, int FinishedMatches);

/// <summary>
/// One line of the top-scorers table. <paramref name="Rank"/> is a dense rank: players with the
/// same number of goals share it.
/// </summary>
public sealed record TopScorerResponse(
    int Rank,
    Guid PlayerId,
    string FirstName,
    string LastName,
    int JerseyNumber,
    Guid TeamId,
    string TeamName,
    string TeamCode,
    int Goals,
    int MatchesScoredIn)
{
    public string FullName => $"{FirstName} {LastName}";
}
