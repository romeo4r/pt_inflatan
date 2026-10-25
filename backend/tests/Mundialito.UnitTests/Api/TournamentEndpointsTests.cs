using System.Net;
using System.Text.Json;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Tournament;
using Mundialito.Application.Tournament.Queries.GetStandings;
using Mundialito.Application.Tournament.Queries.GetTopScorers;
using Mundialito.Domain.Common;
using NSubstitute;

namespace Mundialito.UnitTests.Api;

public sealed class TournamentEndpointsTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();
    private readonly HttpClient _client;

    public TournamentEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetStandings_ShouldReturnRowsInOrder()
    {
        var teamId = Guid.CreateVersion7();
        var standings = new StandingsResponse(
            [new StandingRowResponse(1, teamId, "Tecnología", "TEC", 1, 1, 0, 0, 2, 0, 2, 3)],
            FinishedMatches: 1);
        _factory.Sender
            .Send(Arg.Any<GetStandingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(standings));

        var response = await _client.GetAsync("/api/standings");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("finishedMatches").GetInt32().ShouldBe(1);
        var row = body.RootElement.GetProperty("rows")[0];
        row.GetProperty("position").GetInt32().ShouldBe(1);
        row.GetProperty("teamCode").GetString().ShouldBe("TEC");
        row.GetProperty("points").GetInt32().ShouldBe(3);
        row.GetProperty("goalDifference").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task GetTopScorers_ShouldBindTeamAndPagingAndExposeFullName()
    {
        GetTopScorersQuery? captured = null;
        var teamId = Guid.CreateVersion7();
        var scorer = new TopScorerResponse(1, Guid.CreateVersion7(), "Carlos", "Gómez", 9, teamId, "Finanzas", "FIN", 4, 3);
        _factory.Sender
            .Send(Arg.Do<GetTopScorersQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PagedResult<TopScorerResponse>([scorer], 2, 5, 7)));

        var response = await _client.GetAsync($"/api/top-scorers?teamId={teamId}&pageNumber=2&pageSize=5");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.TeamId.ShouldBe(teamId);
        captured.PageNumber.ShouldBe(2);
        captured.PageSize.ShouldBe(5);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalRecords").GetInt32().ShouldBe(7);
        var first = body.RootElement.GetProperty("data")[0];
        first.GetProperty("rank").GetInt32().ShouldBe(1);
        first.GetProperty("fullName").GetString().ShouldBe("Carlos Gómez");
        first.GetProperty("goals").GetInt32().ShouldBe(4);
    }
}
