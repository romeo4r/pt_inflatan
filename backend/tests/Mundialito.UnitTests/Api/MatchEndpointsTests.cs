using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Matches;
using Mundialito.Application.Matches.Commands.CancelMatch;
using Mundialito.Application.Matches.Commands.DeleteMatch;
using Mundialito.Application.Matches.Commands.RegisterMatchResult;
using Mundialito.Application.Matches.Commands.ScheduleMatch;
using Mundialito.Application.Matches.Queries.GetMatchById;
using Mundialito.Application.Matches.Queries.GetMatches;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;
using NSubstitute;

namespace Mundialito.UnitTests.Api;

public sealed class MatchEndpointsTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime KickOff = new(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);

    private readonly ApiTestFactory _factory = new();
    private readonly HttpClient _client;

    public MatchEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetMatches_ShouldBindFiltersIncludingStatusAndDates()
    {
        GetMatchesQuery? captured = null;
        _factory.Sender
            .Send(Arg.Do<GetMatchesQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(Result.Success(PagedResult<MatchResponse>.Empty(1, 10)));
        var teamId = Guid.CreateVersion7();

        var response = await _client.GetAsync(
            $"/api/matches?status=FINISHED&teamId={teamId}&matchDay=2&fromUtc=2026-11-01T00:00:00Z&toUtc=2026-11-30T00:00:00Z&sortBy=matchDay&sortDirection=desc");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.Status.ShouldBe(MatchStatus.Finished);
        captured.TeamId.ShouldBe(teamId);
        captured.MatchDay.ShouldBe(2);
        captured.FromUtc.ShouldBe(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        captured.FromUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        captured.ToUtc.ShouldBe(new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc));
        captured.SortBy.ShouldBe("matchDay");
    }

    [Fact]
    public async Task GetMatchById_ShouldReturnDetailWithGoalsAndETag()
    {
        var detail = SampleDetail();
        _factory.Sender
            .Send(Arg.Any<GetMatchByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));

        var response = await _client.GetAsync($"/api/matches/{detail.Match.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("match").GetProperty("status").GetString().ShouldBe("Scheduled");
        body.RootElement.GetProperty("goals").GetArrayLength().ShouldBe(1);
        body.RootElement.GetProperty("goals")[0].GetProperty("playerFullName").GetString().ShouldBe("Carlos Gómez");
    }

    [Fact]
    public async Task ScheduleMatch_ShouldNormalizeDateToUtcAndReturn201()
    {
        var detail = SampleDetail();
        ScheduleMatchCommand? captured = null;
        _factory.Sender
            .Send(Arg.Do<ScheduleMatchCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail.Match.Id));
        _factory.Sender
            .Send(Arg.Any<GetMatchByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/matches")
        {
            // Sent with an offset of -06:00: must be stored as 21:00 UTC.
            Content = JsonContent.Create(new
            {
                homeTeamId = detail.Match.HomeTeamId,
                awayTeamId = detail.Match.AwayTeamId,
                matchDay = 1,
                scheduledAtUtc = "2026-11-07T15:00:00-06:00",
                venue = "Cancha 1",
            }),
        };
        request.Headers.Add(IdempotencyMiddleware.HeaderName, "match-key-1");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldBe($"/api/matches/{detail.Match.Id}");
        captured.ShouldNotBeNull();
        captured.ScheduledAtUtc.ShouldBe(new DateTime(2026, 11, 7, 21, 0, 0, DateTimeKind.Utc));
        captured.ScheduledAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ScheduleMatch_WithoutIdempotencyKey_ShouldReturn400()
    {
        var response = await _client.PostAsJsonAsync("/api/matches", new { matchDay = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _factory.Sender.DidNotReceive().Send(Arg.Any<ScheduleMatchCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelMatch_ShouldReturnFreshDetail()
    {
        var detail = SampleDetail();
        _factory.Sender
            .Send(Arg.Any<CancelMatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _factory.Sender
            .Send(Arg.Any<GetMatchByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));

        var response = await _client.PostAsync($"/api/matches/{detail.Match.Id}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.Sender.Received(1).Send(
            Arg.Is<CancelMatchCommand>(c => c.Id == detail.Match.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelMatch_OnFinishedMatch_ShouldReturn409()
    {
        _factory.Sender
            .Send(Arg.Any<CancelMatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(MatchErrors.AlreadyFinished));

        var response = await _client.PostAsync($"/api/matches/{Guid.CreateVersion7()}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe("Match.AlreadyFinished");
    }

    [Fact]
    public async Task RegisterResult_ShouldBindGoalsAndIfMatchAndReturnDetail()
    {
        var detail = SampleDetail();
        RegisterMatchResultCommand? captured = null;
        _factory.Sender
            .Send(Arg.Do<RegisterMatchResultCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _factory.Sender
            .Send(Arg.Any<GetMatchByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));
        var scorer = Guid.CreateVersion7();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/matches/{detail.Match.Id}/result")
        {
            Content = JsonContent.Create(new
            {
                homeScore = 1,
                awayScore = 0,
                goals = new[] { new { playerId = scorer, minute = 33 } },
            }),
        };
        request.Headers.Add(IdempotencyMiddleware.HeaderName, "result-key-1");
        request.Headers.TryAddWithoutValidation("If-Match", "\"AAAAAAAAB9E=\"");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        captured.ShouldNotBeNull();
        captured.MatchId.ShouldBe(detail.Match.Id);
        captured.HomeScore.ShouldBe(1);
        captured.Goals.ShouldHaveSingleItem().ShouldBe(new GoalInput(scorer, 33));
        captured.ExpectedRowVersion.ShouldBe([0, 0, 0, 0, 0, 0, 7, 209]);
    }

    [Fact]
    public async Task RegisterResult_WithoutGoalsProperty_ShouldSendEmptyList()
    {
        RegisterMatchResultCommand? captured = null;
        _factory.Sender
            .Send(Arg.Do<RegisterMatchResultCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(MatchErrors.AlreadyFinished));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/matches/{Guid.CreateVersion7()}/result")
        {
            Content = JsonContent.Create(new { homeScore = 0, awayScore = 0 }),
        };
        request.Headers.Add(IdempotencyMiddleware.HeaderName, "result-key-2");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        captured.ShouldNotBeNull().Goals.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegisterResult_WithoutIdempotencyKey_ShouldReturn400()
    {
        var response = await _client.PostAsJsonAsync($"/api/matches/{Guid.CreateVersion7()}/result", new { homeScore = 0, awayScore = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _factory.Sender.DidNotReceive().Send(Arg.Any<RegisterMatchResultCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteMatch_ShouldReturn204()
    {
        _factory.Sender
            .Send(Arg.Any<DeleteMatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var response = await _client.DeleteAsync($"/api/matches/{Guid.CreateVersion7()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static MatchDetailResponse SampleDetail()
    {
        var home = Guid.CreateVersion7();
        var away = Guid.CreateVersion7();
        var match = new MatchResponse(
            Guid.CreateVersion7(), 1, KickOff, "Cancha 1", MatchStatus.Scheduled,
            home, "Finanzas", "FIN", away, "Tecnología", "TEC",
            null, null, null, Now, null, [0, 0, 0, 0, 0, 0, 7, 209]);
        var goal = new GoalResponse(Guid.CreateVersion7(), Guid.CreateVersion7(), "Carlos", "Gómez", home, "FIN", 12);

        return new MatchDetailResponse(match, [goal]);
    }
}
