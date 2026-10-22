using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Matches;
using Mundialito.Application.Matches.Queries.GetMatchById;
using Mundialito.Application.Matches.Queries.GetMatches;
using Mundialito.Application.Players;
using Mundialito.Application.Players.Queries.GetPlayerById;
using Mundialito.Application.Teams;
using Mundialito.Application.Teams.Queries.GetTeamById;
using Mundialito.Application.Teams.Queries.GetTeams;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;
using NSubstitute;

namespace Mundialito.UnitTests.Application.Queries;

public class QueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetTeams_ShouldReturnPageFromReadStore()
    {
        var store = Substitute.For<ITeamReadStore>();
        var query = new GetTeamsQuery { PageSize = 2, SortBy = "code" };
        var page = new PagedResult<TeamResponse>([SampleTeam()], 1, 2, 5);
        store.GetPagedAsync(query, Arg.Any<CancellationToken>()).Returns(page);

        var result = await new GetTeamsQueryHandler(store).Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(page);
        result.Value.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task GetTeamById_WhenMissing_ShouldReturnNotFound()
    {
        var store = Substitute.For<ITeamReadStore>();
        var id = Guid.CreateVersion7();
        store.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((TeamResponse?)null);

        var result = await new GetTeamByIdQueryHandler(store).Handle(new GetTeamByIdQuery(id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Team.NotFound");
    }

    [Fact]
    public async Task GetTeamById_WhenFound_ShouldReturnTeam()
    {
        var store = Substitute.For<ITeamReadStore>();
        var team = SampleTeam();
        store.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);

        var result = await new GetTeamByIdQueryHandler(store).Handle(new GetTeamByIdQuery(team.Id), CancellationToken.None);

        result.Value.ShouldBe(team);
    }

    [Fact]
    public async Task GetPlayerById_WhenMissing_ShouldReturnNotFound()
    {
        var store = Substitute.For<IPlayerReadStore>();
        var id = Guid.CreateVersion7();
        store.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((PlayerResponse?)null);

        var result = await new GetPlayerByIdQueryHandler(store).Handle(new GetPlayerByIdQuery(id), CancellationToken.None);

        result.Error.Code.ShouldBe("Player.NotFound");
    }

    [Fact]
    public async Task GetMatchById_WhenMissing_ShouldReturnNotFound()
    {
        var store = Substitute.For<IMatchReadStore>();
        var id = Guid.CreateVersion7();
        store.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((MatchDetailResponse?)null);

        var result = await new GetMatchByIdQueryHandler(store).Handle(new GetMatchByIdQuery(id), CancellationToken.None);

        result.Error.Code.ShouldBe("Match.NotFound");
    }

    [Fact]
    public async Task GetMatches_ShouldPassQueryThroughUnchanged()
    {
        var store = Substitute.For<IMatchReadStore>();
        var query = new GetMatchesQuery { Status = MatchStatus.Finished, PageNumber = 2 };
        store.GetPagedAsync(query, Arg.Any<CancellationToken>()).Returns(PagedResult<MatchResponse>.Empty(2, 10));

        var result = await new GetMatchesQueryHandler(store).Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await store.Received(1).GetPagedAsync(query, Arg.Any<CancellationToken>());
    }

    private static TeamResponse SampleTeam() =>
        new(Guid.CreateVersion7(), "Finanzas", "FIN", null, true, 5, Now, null);
}
