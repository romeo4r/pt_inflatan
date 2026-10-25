using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Domain.Common;
using Mundialito.Domain.Tournament;

namespace Mundialito.Application.Tournament.Queries.GetStandings;

/// <summary>
/// Fetches the raw material (teams and finished matches) through the read store and lets the
/// domain compute the ranking, so the rules are unit-tested once and never duplicated in SQL.
/// </summary>
internal sealed class GetStandingsQueryHandler(ITournamentReadStore readStore)
    : IQueryHandler<GetStandingsQuery, StandingsResponse>
{
    public async Task<Result<StandingsResponse>> Handle(GetStandingsQuery query, CancellationToken cancellationToken)
    {
        var source = await readStore.GetStandingsSourceAsync(cancellationToken);

        var rows = StandingsCalculator.Calculate(source.Teams, source.Matches)
            .Select(ToResponse)
            .ToList();

        return new StandingsResponse(rows, source.Matches.Count);
    }

    private static StandingRowResponse ToResponse(StandingRow row) => new(
        row.Position, row.TeamId, row.TeamName, row.TeamCode,
        row.Played, row.Won, row.Drawn, row.Lost,
        row.GoalsFor, row.GoalsAgainst, row.GoalDifference, row.Points);
}
