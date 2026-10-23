using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Teams;
using Mundialito.Application.Teams.Commands.CreateTeam;
using Mundialito.Application.Teams.Commands.DeleteTeam;
using Mundialito.Application.Teams.Commands.PatchTeam;
using Mundialito.Application.Teams.Commands.UpdateTeam;
using Mundialito.Application.Teams.Queries.GetTeamById;
using Mundialito.Application.Teams.Queries.GetTeams;
using Mundialito.Domain.Common;
using Mundialito.Domain.Teams;
using NSubstitute;
using SortDirection = Mundialito.Application.Common.Pagination.SortDirection;

namespace Mundialito.UnitTests.Api;

public sealed class TeamEndpointsTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Version = [0, 0, 0, 0, 0, 0, 7, 209];

    private readonly ApiTestFactory _factory = new();
    private readonly HttpClient _client;

    public TeamEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---------- GET ----------

    [Fact]
    public async Task GetTeams_ShouldBindQueryStringAndReturnPage()
    {
        GetTeamsQuery? captured = null;
        _factory.Sender
            .Send(Arg.Do<GetTeamsQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PagedResult<TeamResponse>([SampleTeam()], 2, 5, 11)));

        var response = await _client.GetAsync("/api/teams?search=fin&isActive=true&pageNumber=2&pageSize=5&sortBy=code&sortDirection=DESC");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.Search.ShouldBe("fin");
        captured.IsActive.ShouldBe(true);
        captured.PageNumber.ShouldBe(2);
        captured.PageSize.ShouldBe(5);
        captured.SortBy.ShouldBe("code");
        captured.SortDirection.ShouldBe(SortDirection.Desc);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalRecords").GetInt32().ShouldBe(11);
        body.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(3);
        body.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task GetTeams_WithoutQueryString_ShouldUseDefaults()
    {
        GetTeamsQuery? captured = null;
        _factory.Sender
            .Send(Arg.Do<GetTeamsQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(Result.Success(PagedResult<TeamResponse>.Empty(1, 10)));

        await _client.GetAsync("/api/teams");

        captured.ShouldNotBeNull();
        captured.PageNumber.ShouldBe(PagingDefaults.DefaultPageNumber);
        captured.PageSize.ShouldBe(PagingDefaults.DefaultPageSize);
        captured.SortDirection.ShouldBe(SortDirection.Asc);
    }

    [Fact]
    public async Task GetTeamById_ShouldReturnETagHeaderAndRowVersionInBody()
    {
        var team = SampleTeam();
        _factory.Sender
            .Send(Arg.Any<GetTeamByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(team));

        var response = await _client.GetAsync($"/api/teams/{team.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.ShouldBe("\"AAAAAAAAB9E=\"");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("rowVersion").GetString().ShouldBe("AAAAAAAAB9E=");
    }

    [Fact]
    public async Task GetTeamById_WhenNotFound_ShouldReturn404ProblemDetails()
    {
        var id = Guid.CreateVersion7();
        _factory.Sender
            .Send(Arg.Any<GetTeamByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<TeamResponse>(TeamErrors.NotFound(id)));

        var response = await _client.GetAsync($"/api/teams/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe("Team.NotFound");
        body.RootElement.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain(id.ToString());
        body.RootElement.TryGetProperty("correlationId", out _).ShouldBeTrue();
    }

    // ---------- POST + idempotency ----------

    [Fact]
    public async Task CreateTeam_ShouldReturn201WithLocationAndBody()
    {
        var team = SampleTeam();
        SetupCreate(team);

        var response = await PostTeamAsync(new { name = "Finanzas", code = "FIN" }, "key-1");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldBe($"/api/teams/{team.Id}");
        var body = await response.Content.ReadFromJsonAsync<TeamResponse>();
        body.ShouldNotBeNull().Id.ShouldBe(team.Id);
    }

    [Fact]
    public async Task CreateTeam_WithoutIdempotencyKey_ShouldReturn400AndNotReachHandler()
    {
        var response = await _client.PostAsJsonAsync("/api/teams", new { name = "Finanzas", code = "FIN" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe("Idempotency.KeyRequired");
        await _factory.Sender.DidNotReceive().Send(Arg.Any<CreateTeamCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTeam_RepeatedWithSameKey_ShouldReplayOriginalResponseWithoutSecondCreate()
    {
        var team = SampleTeam();
        SetupCreate(team);
        var payload = new { name = "Finanzas", code = "FIN" };

        var first = await PostTeamAsync(payload, "key-replay");
        var second = await PostTeamAsync(payload, "key-replay");

        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.Location!.ToString().ShouldBe(first.Headers.Location!.ToString());
        second.Headers.GetValues(IdempotencyMiddleware.ReplayedHeaderName).Single().ShouldBe("true");
        (await second.Content.ReadAsStringAsync()).ShouldBe(await first.Content.ReadAsStringAsync());
        await _factory.Sender.Received(1).Send(Arg.Any<CreateTeamCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTeam_SameKeyDifferentPayload_ShouldReturn409()
    {
        SetupCreate(SampleTeam());

        await PostTeamAsync(new { name = "Finanzas", code = "FIN" }, "key-mismatch");
        var response = await PostTeamAsync(new { name = "Tecnología", code = "TEC" }, "key-mismatch");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe("Idempotency.PayloadMismatch");
    }

    [Fact]
    public async Task CreateTeam_WithValidationError_ShouldReturn400WithFieldErrors()
    {
        var error = new ValidationError(new Dictionary<string, string[]> { ["name"] = ["El nombre del equipo es obligatorio."] });
        _factory.Sender
            .Send(Arg.Any<CreateTeamCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(error));

        var response = await PostTeamAsync(new { name = "", code = "FIN" }, "key-invalid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe(ValidationError.DefaultCode);
        body.RootElement.GetProperty("errors").GetProperty("name")[0].GetString().ShouldBe("El nombre del equipo es obligatorio.");
    }

    [Fact]
    public async Task CreateTeam_WithConflict_ShouldReturn409()
    {
        _factory.Sender
            .Send(Arg.Any<CreateTeamCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(TeamErrors.CodeAlreadyExists("FIN")));

        var response = await PostTeamAsync(new { name = "Finanzas", code = "FIN" }, "key-conflict");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---------- PUT / PATCH + If-Match ----------

    [Fact]
    public async Task UpdateTeam_ShouldSendCommandWithRouteIdAndReturn200()
    {
        var team = SampleTeam();
        UpdateTeamCommand? captured = null;
        _factory.Sender
            .Send(Arg.Do<UpdateTeamCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _factory.Sender
            .Send(Arg.Any<GetTeamByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(team));

        var response = await _client.PutAsJsonAsync($"/api/teams/{team.Id}", new { name = "Finanzas", code = "FIN", coach = "Ana", isActive = false });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        captured.ShouldNotBeNull();
        captured.Id.ShouldBe(team.Id);
        captured.IsActive.ShouldBeFalse();
        captured.ExpectedRowVersion.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateTeam_WithIfMatch_ShouldPassExpectedVersionToCommand()
    {
        var team = SampleTeam();
        UpdateTeamCommand? captured = null;
        _factory.Sender
            .Send(Arg.Do<UpdateTeamCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _factory.Sender
            .Send(Arg.Any<GetTeamByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(team));
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/teams/{team.Id}")
        {
            Content = JsonContent.Create(new { name = "Finanzas", code = "FIN" }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"AAAAAAAAB9E=\"");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull().ExpectedRowVersion.ShouldBe(Version);
    }

    [Fact]
    public async Task UpdateTeam_WithStaleIfMatch_ShouldReturn409()
    {
        _factory.Sender
            .Send(Arg.Any<UpdateTeamCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(PersistenceErrors.ConcurrencyConflict));
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/teams/{Guid.CreateVersion7()}")
        {
            Content = JsonContent.Create(new { name = "Finanzas", code = "FIN" }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"AAAAAAAAB9E=\"");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe("Concurrency.Conflict");
    }

    [Fact]
    public async Task UpdateTeam_WithMalformedIfMatch_ShouldReturn400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/teams/{Guid.CreateVersion7()}")
        {
            Content = JsonContent.Create(new { name = "Finanzas", code = "FIN" }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"???\"");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _factory.Sender.DidNotReceive().Send(Arg.Any<UpdateTeamCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PatchTeam_ShouldOnlyCarryProvidedFields()
    {
        var team = SampleTeam();
        PatchTeamCommand? captured = null;
        _factory.Sender
            .Send(Arg.Do<PatchTeamCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _factory.Sender
            .Send(Arg.Any<GetTeamByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(team));

        var response = await _client.PatchAsJsonAsync($"/api/teams/{team.Id}", new { coach = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.Coach.ShouldBe("");
        captured.Name.ShouldBeNull();
        captured.IsActive.ShouldBeNull();
    }

    // ---------- DELETE ----------

    [Fact]
    public async Task DeleteTeam_ShouldReturn204()
    {
        _factory.Sender
            .Send(Arg.Any<DeleteTeamCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var response = await _client.DeleteAsync($"/api/teams/{Guid.CreateVersion7()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTeam_WithMatches_ShouldReturn409()
    {
        _factory.Sender
            .Send(Arg.Any<DeleteTeamCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(TeamErrors.HasMatches));

        var response = await _client.DeleteAsync($"/api/teams/{Guid.CreateVersion7()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnyRequest_ShouldEchoCorrelationIdHeader()
    {
        _factory.Sender
            .Send(Arg.Any<GetTeamsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(PagedResult<TeamResponse>.Empty(1, 10)));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/teams");
        request.Headers.Add("X-Correlation-Id", "test-corr-1");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("test-corr-1");
        response.Headers.Contains("X-Trace-Id").ShouldBeTrue();
    }

    private void SetupCreate(TeamResponse team)
    {
        _factory.Sender
            .Send(Arg.Any<CreateTeamCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(team.Id));
        _factory.Sender
            .Send(Arg.Any<GetTeamByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(team));
    }

    private async Task<HttpResponseMessage> PostTeamAsync(object payload, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/teams")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add(IdempotencyMiddleware.HeaderName, idempotencyKey);

        return await _client.SendAsync(request);
    }

    private static TeamResponse SampleTeam() =>
        new(Guid.CreateVersion7(), "Finanzas", "FIN", null, true, 0, Now, null, Version);
}
