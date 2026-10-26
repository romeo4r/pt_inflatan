using System.Net;

namespace Mundialito.IntegrationTests;

/// <summary>
/// End-to-end write scenarios on a real database: idempotent POST, optimistic concurrency,
/// unique violations and the audit trail linked by correlation id.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class WriteSideTests(SqlServerFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task CreateTeam_ReplayedWithSameKey_ShouldNotCreateASecondTeam()
    {
        var code = UniqueCode();
        var key = HttpExtensions.NewKey();
        var body = new { name = $"Equipo {code}", code, coach = "Ana" };

        var first = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/teams").WithJson(body).WithHeader("Idempotency-Key", key));
        var second = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/teams").WithJson(body).WithHeader("Idempotency-Key", key));

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.GetValues("Idempotent-Replayed").Single().ShouldBe("true");
        (await second.Content.ReadAsStringAsync()).ShouldBe(await first.Content.ReadAsStringAsync());

        using var search = await (await _client.GetAsync($"/api/teams?search={code}")).ReadJsonAsync();
        search.RootElement.GetProperty("totalRecords").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task CreateTeam_WithDuplicateCode_ShouldReturn409()
    {
        var code = UniqueCode();
        await CreateTeamAsync(code);

        var duplicate = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/teams")
            .WithJson(new { name = $"Otro {code}", code })
            .WithHeader("Idempotency-Key", HttpExtensions.NewKey()));

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var problem = await duplicate.ReadJsonAsync();
        problem.RootElement.GetProperty("code").GetString().ShouldBe("Team.CodeAlreadyExists");
    }

    [Fact]
    public async Task UpdateTeam_WithStaleETag_ShouldReturn409ThenSucceedWithFreshOne()
    {
        var code = UniqueCode();
        var teamId = await CreateTeamAsync(code);
        var initial = await _client.GetAsync($"/api/teams/{teamId}");
        var staleETag = initial.Headers.ETag!.Tag;

        // First update succeeds and changes the row version.
        var ok = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/teams/{teamId}")
            .WithJson(new { name = $"Equipo {code}", code, coach = "Luis", isActive = true })
            .WithHeader("If-Match", staleETag));
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        var freshETag = ok.Headers.ETag!.Tag;
        freshETag.ShouldNotBe(staleETag);

        // Same stale ETag again: someone else already changed it.
        var stale = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/teams/{teamId}")
            .WithJson(new { name = $"Equipo {code}", code, coach = "Carlos", isActive = true })
            .WithHeader("If-Match", staleETag));
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var problem = await stale.ReadJsonAsync();
        problem.RootElement.GetProperty("code").GetString().ShouldBe("Concurrency.Conflict");

        var fresh = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/teams/{teamId}")
            .WithJson(new { name = $"Equipo {code}", code, coach = "Carlos", isActive = true })
            .WithHeader("If-Match", freshETag));
        fresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteTeam_ShouldBeIdempotentAndRemovePlayers()
    {
        var code = UniqueCode();
        var teamId = await CreateTeamAsync(code);
        var player = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{teamId}/players")
            .WithJson(new { firstName = "Mario", lastName = "Rivas", jerseyNumber = 1, position = "Goalkeeper" })
            .WithHeader("Idempotency-Key", HttpExtensions.NewKey()));
        player.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await _client.DeleteAsync($"/api/teams/{teamId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/teams/{teamId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.GetAsync($"/api/teams/{teamId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var players = await (await _client.GetAsync($"/api/teams/{teamId}/players")).ReadJsonAsync();
        players.RootElement.GetProperty("totalRecords").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task RegisterResult_ShouldUpdateStandingsAndLeaveATraceableEvent()
    {
        // A fresh pair of teams keeps this scenario independent from the seeded table.
        var homeCode = UniqueCode();
        var awayCode = UniqueCode();
        var homeId = await CreateTeamAsync(homeCode);
        var awayId = await CreateTeamAsync(awayCode);
        var scorerId = await CreatePlayerAsync(homeId, 9, "Forward");

        var scheduled = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/matches")
            .WithJson(new { homeTeamId = homeId, awayTeamId = awayId, matchDay = 9, scheduledAtUtc = "2026-12-01T15:00:00Z", venue = "Cancha 3" })
            .WithHeader("Idempotency-Key", HttpExtensions.NewKey()));
        scheduled.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await scheduled.ReadJsonAsync();
        var matchId = created.RootElement.GetProperty("match").GetProperty("id").GetGuid();

        var correlationId = $"it-{HttpExtensions.NewKey()}";
        var result = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/matches/{matchId}/result")
            .WithJson(new { homeScore = 1, awayScore = 0, goals = new[] { new { playerId = scorerId, minute = 40 } } })
            .WithHeader("Idempotency-Key", HttpExtensions.NewKey())
            .WithHeader("X-Correlation-Id", correlationId));
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var detail = await result.ReadJsonAsync();
        detail.RootElement.GetProperty("match").GetProperty("status").GetString().ShouldBe("Finished");
        detail.RootElement.GetProperty("goals").GetArrayLength().ShouldBe(1);

        // Registering again is a conflict, not a duplicate.
        var again = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/matches/{matchId}/result")
            .WithJson(new { homeScore = 0, awayScore = 0, goals = Array.Empty<object>() })
            .WithHeader("Idempotency-Key", HttpExtensions.NewKey()));
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var standings = await (await _client.GetAsync("/api/standings")).ReadJsonAsync();
        var homeRow = standings.RootElement.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("teamId").GetGuid() == homeId);
        homeRow.GetProperty("points").GetInt32().ShouldBe(3);
        homeRow.GetProperty("goalDifference").GetInt32().ShouldBe(1);

        using var scorers = await (await _client.GetAsync($"/api/top-scorers?teamId={homeId}")).ReadJsonAsync();
        scorers.RootElement.GetProperty("data")[0].GetProperty("playerId").GetGuid().ShouldBe(scorerId);

        using var events = await (await _client.GetAsync($"/api/domain-events?correlationId={correlationId}")).ReadJsonAsync();
        var trace = events.RootElement.GetProperty("data").EnumerateArray().Single();
        trace.GetProperty("eventType").GetString().ShouldBe("MatchResultRegisteredDomainEvent");
        trace.GetProperty("aggregateId").GetGuid().ShouldBe(matchId);
    }

    private async Task<Guid> CreateTeamAsync(string code)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/teams")
            .WithJson(new { name = $"Equipo {code}", code })
            .WithHeader("Idempotency-Key", HttpExtensions.NewKey()));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var body = await response.ReadJsonAsync();
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreatePlayerAsync(Guid teamId, int jersey, string position)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{teamId}/players")
            .WithJson(new { firstName = "Carlos", lastName = "Gómez", jerseyNumber = jersey, position })
            .WithHeader("Idempotency-Key", HttpExtensions.NewKey()));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var body = await response.ReadJsonAsync();
        return body.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Codes are unique per team; a short random suffix keeps tests independent.</summary>
    private static string UniqueCode() => "T" + Guid.CreateVersion7().ToString("N")[^6..].ToUpperInvariant();
}
