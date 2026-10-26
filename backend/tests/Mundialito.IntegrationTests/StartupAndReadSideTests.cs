using System.Net;

namespace Mundialito.IntegrationTests;

/// <summary>
/// Verifies what unit tests cannot: migrations apply on a real SQL Server, the seed runs through
/// the real Unit of Work, and every Dapper query maps its columns (types included) correctly.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class StartupAndReadSideTests(SqlServerFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task Health_ShouldReportDatabaseHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await response.ReadJsonAsync();
        body.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
        body.RootElement.GetProperty("checks")[0].GetProperty("name").GetString().ShouldBe("database");
    }

    [Fact]
    public async Task Seed_ShouldBeVisibleThroughTheReadSide()
    {
        // Other tests create teams too, so pin the checks to a seeded team (code FIN).
        using var teams = await (await _client.GetAsync("/api/teams?search=FIN&pageSize=50")).ReadJsonAsync();
        var fin = teams.RootElement.GetProperty("data").EnumerateArray().Single(t => t.GetProperty("code").GetString() == "FIN");
        var finId = fin.GetProperty("id").GetGuid();
        using var players = await (await _client.GetAsync($"/api/teams/{finId}/players?pageSize=50")).ReadJsonAsync();
        using var matches = await (await _client.GetAsync("/api/matches?pageSize=50")).ReadJsonAsync();

        fin.GetProperty("playerCount").GetInt32().ShouldBe(5);
        players.RootElement.GetProperty("totalRecords").GetInt32().ShouldBe(5);
        matches.RootElement.GetProperty("totalRecords").GetInt32().ShouldBeGreaterThanOrEqualTo(6);

        // Column/type mapping: date -> DateOnly, rowversion -> base64, enum -> name.
        var player = players.RootElement.GetProperty("data")[0];
        player.GetProperty("birthDate").GetString().ShouldNotBeNull().ShouldMatch(@"^\d{4}-\d{2}-\d{2}$");
        player.GetProperty("rowVersion").GetString().ShouldNotBeNullOrEmpty();
        player.GetProperty("position").GetString().ShouldBeOneOf("Goalkeeper", "Defender", "Midfielder", "Forward");
        fin.GetProperty("rowVersion").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Matches_ShouldFilterByStatusAndTeam()
    {
        using var finished = await (await _client.GetAsync("/api/matches?status=finished&pageSize=50")).ReadJsonAsync();
        var first = finished.RootElement.GetProperty("data")[0];
        var homeTeamId = first.GetProperty("homeTeamId").GetGuid();

        finished.RootElement.GetProperty("data").EnumerateArray()
            .ShouldAllBe(m => m.GetProperty("status").GetString() == "Finished");

        using var byTeam = await (await _client.GetAsync($"/api/matches?teamId={homeTeamId}&pageSize=50")).ReadJsonAsync();
        byTeam.RootElement.GetProperty("data").EnumerateArray().ShouldAllBe(m =>
            m.GetProperty("homeTeamId").GetGuid() == homeTeamId || m.GetProperty("awayTeamId").GetGuid() == homeTeamId);
    }

    [Fact]
    public async Task MatchDetail_ShouldIncludeGoalsAndETag()
    {
        using var finished = await (await _client.GetAsync("/api/matches?status=finished&sortBy=scheduledAt&pageSize=1")).ReadJsonAsync();
        var matchId = finished.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid();

        var response = await _client.GetAsync($"/api/matches/{matchId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        using var body = await response.ReadJsonAsync();
        body.RootElement.GetProperty("match").GetProperty("id").GetGuid().ShouldBe(matchId);
        body.RootElement.GetProperty("goals").GetArrayLength().ShouldBeGreaterThan(0);
        body.RootElement.GetProperty("goals")[0].GetProperty("playerFullName").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task TopScorers_ShouldAggregateWithDenseRank()
    {
        using var body = await (await _client.GetAsync("/api/top-scorers?pageSize=10")).ReadJsonAsync();

        var data = body.RootElement.GetProperty("data");
        data.GetArrayLength().ShouldBeGreaterThan(0);
        data[0].GetProperty("rank").GetInt32().ShouldBe(1);
        data[0].GetProperty("goals").GetInt32().ShouldBeGreaterThanOrEqualTo(data[data.GetArrayLength() - 1].GetProperty("goals").GetInt32());
        data[0].GetProperty("fullName").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SeedEvents_ShouldBeInTheAuditTrail()
    {
        using var body = await (await _client.GetAsync("/api/domain-events?eventType=TeamCreatedDomainEvent&pageSize=50")).ReadJsonAsync();

        body.RootElement.GetProperty("totalRecords").GetInt32().ShouldBeGreaterThanOrEqualTo(4);
        body.RootElement.GetProperty("data")[0].GetProperty("payload").GetString().ShouldNotBeNull().ShouldContain("\"code\"");
    }
}
