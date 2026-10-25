using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Tournament;
using Mundialito.Application.Tournament.Queries.GetStandings;
using Mundialito.Application.Tournament.Queries.GetTopScorers;
using Mundialito.Domain.Tournament;
using NSubstitute;

namespace Mundialito.UnitTests.Application.Tournament;

public class TournamentQueryTests
{
    private readonly ITournamentReadStore _store = Substitute.For<ITournamentReadStore>();

    [Fact]
    public async Task GetStandings_ShouldComputeTableFromReadStoreSource()
    {
        var fin = new TournamentTeam(Guid.CreateVersion7(), "Finanzas", "FIN");
        var tec = new TournamentTeam(Guid.CreateVersion7(), "Tecnología", "TEC");
        _store.GetStandingsSourceAsync(Arg.Any<CancellationToken>()).Returns(new StandingsSource(
            [fin, tec],
            [new PlayedMatch(fin.Id, tec.Id, 0, 2)]));

        var result = await new GetStandingsQueryHandler(_store).Handle(new GetStandingsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.FinishedMatches.ShouldBe(1);
        result.Value.Rows.Count.ShouldBe(2);
        result.Value.Rows[0].TeamCode.ShouldBe("TEC");
        result.Value.Rows[0].Points.ShouldBe(3);
        result.Value.Rows[0].Position.ShouldBe(1);
        result.Value.Rows[1].TeamCode.ShouldBe("FIN");
        result.Value.Rows[1].GoalDifference.ShouldBe(-2);
    }

    [Fact]
    public async Task GetStandings_WithoutTeams_ShouldReturnEmptyTable()
    {
        _store.GetStandingsSourceAsync(Arg.Any<CancellationToken>()).Returns(new StandingsSource([], []));

        var result = await new GetStandingsQueryHandler(_store).Handle(new GetStandingsQuery(), CancellationToken.None);

        result.Value.Rows.ShouldBeEmpty();
        result.Value.FinishedMatches.ShouldBe(0);
    }

    [Fact]
    public async Task GetTopScorers_ShouldPassQueryToReadStore()
    {
        var query = new GetTopScorersQuery { TeamId = Guid.CreateVersion7(), PageSize = 5 };
        var page = new PagedResult<TopScorerResponse>([], 1, 5, 0);
        _store.GetTopScorersAsync(query, Arg.Any<CancellationToken>()).Returns(page);

        var result = await new GetTopScorersQueryHandler(_store).Handle(query, CancellationToken.None);

        result.Value.ShouldBeSameAs(page);
    }

    [Fact]
    public void GetTopScorersValidator_ShouldAcceptDefaultsAndRejectBadPaging()
    {
        var validator = new GetTopScorersQueryValidator();

        validator.Validate(new GetTopScorersQuery()).IsValid.ShouldBeTrue();
        validator.Validate(new GetTopScorersQuery { PageSize = 0 }).IsValid.ShouldBeFalse();
        validator.Validate(new GetTopScorersQuery { SortBy = "name" }).IsValid.ShouldBeFalse();
    }
}
