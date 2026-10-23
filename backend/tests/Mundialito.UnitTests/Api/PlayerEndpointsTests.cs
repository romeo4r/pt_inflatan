using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Players;
using Mundialito.Application.Players.Commands.CreatePlayer;
using Mundialito.Application.Players.Commands.DeletePlayer;
using Mundialito.Application.Players.Commands.PatchPlayer;
using Mundialito.Application.Players.Queries.GetPlayerById;
using Mundialito.Application.Players.Queries.GetPlayers;
using Mundialito.Domain.Common;
using Mundialito.Domain.Players;
using NSubstitute;

namespace Mundialito.UnitTests.Api;

public sealed class PlayerEndpointsTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeamId = Guid.CreateVersion7();

    private readonly ApiTestFactory _factory = new();
    private readonly HttpClient _client;

    public PlayerEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetTeamPlayers_ShouldScopeQueryToRouteTeam()
    {
        GetPlayersQuery? captured = null;
        _factory.Sender
            .Send(Arg.Do<GetPlayersQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(Result.Success(PagedResult<PlayerResponse>.Empty(1, 10)));

        var response = await _client.GetAsync($"/api/teams/{TeamId}/players?position=FORWARD&sortBy=jerseyNumber");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.TeamId.ShouldBe(TeamId);
        captured.Position.ShouldBe(PlayerPosition.Forward);
        captured.SortBy.ShouldBe("jerseyNumber");
    }

    [Fact]
    public async Task GetPlayers_WithInvalidPosition_ShouldForwardSentinelSoValidatorRejectsIt()
    {
        GetPlayersQuery? captured = null;
        _factory.Sender
            .Send(Arg.Do<GetPlayersQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(Result.Success(PagedResult<PlayerResponse>.Empty(1, 10)));

        await _client.GetAsync("/api/players?position=striker");

        captured.ShouldNotBeNull();
        captured.Position.ShouldNotBeNull();
        Enum.IsDefined(captured.Position.Value).ShouldBeFalse();
    }

    [Fact]
    public async Task CreatePlayer_ShouldUseRouteTeamAndReturn201()
    {
        var player = SamplePlayer();
        CreatePlayerCommand? captured = null;
        _factory.Sender
            .Send(Arg.Do<CreatePlayerCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Result.Success(player.Id));
        _factory.Sender
            .Send(Arg.Any<GetPlayerByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(player));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{TeamId}/players")
        {
            Content = JsonContent.Create(new { firstName = "Carlos", lastName = "Gómez", jerseyNumber = 9, position = "forward", birthDate = "1995-05-20" }),
        };
        request.Headers.Add(IdempotencyMiddleware.HeaderName, "player-key-1");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldBe($"/api/players/{player.Id}");
        captured.ShouldNotBeNull();
        captured.TeamId.ShouldBe(TeamId);
        captured.Position.ShouldBe(PlayerPosition.Forward);
        captured.BirthDate.ShouldBe(new DateOnly(1995, 5, 20));
    }

    [Fact]
    public async Task CreatePlayer_WithoutIdempotencyKey_ShouldReturn400()
    {
        var response = await _client.PostAsJsonAsync($"/api/teams/{TeamId}/players", new { firstName = "Carlos" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _factory.Sender.DidNotReceive().Send(Arg.Any<CreatePlayerCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPlayerById_ShouldSerializePositionAsStringAndReturnETag()
    {
        var player = SamplePlayer();
        _factory.Sender
            .Send(Arg.Any<GetPlayerByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(player));

        var response = await _client.GetAsync($"/api/players/{player.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("position").GetString().ShouldBe("Forward");
        body.RootElement.GetProperty("fullName").GetString().ShouldBe("Carlos Gómez");
        body.RootElement.GetProperty("birthDate").GetString().ShouldBe("1995-05-20");
    }

    [Fact]
    public async Task PatchPlayer_ShouldCarryOnlyProvidedFields()
    {
        var player = SamplePlayer();
        PatchPlayerCommand? captured = null;
        _factory.Sender
            .Send(Arg.Do<PatchPlayerCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _factory.Sender
            .Send(Arg.Any<GetPlayerByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(player));

        var response = await _client.PatchAsJsonAsync($"/api/players/{player.Id}", new { isActive = false });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.IsActive.ShouldBe(false);
        captured.FirstName.ShouldBeNull();
        captured.JerseyNumber.ShouldBeNull();
        captured.Position.ShouldBeNull();
    }

    [Fact]
    public async Task DeletePlayer_WithGoals_ShouldReturn409()
    {
        _factory.Sender
            .Send(Arg.Any<DeletePlayerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(PlayerErrors.HasGoals));

        var response = await _client.DeleteAsync($"/api/players/{Guid.CreateVersion7()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe("Player.HasGoals");
    }

    private static PlayerResponse SamplePlayer() => new(
        Guid.CreateVersion7(), TeamId, "Finanzas", "FIN", "Carlos", "Gómez", 9, PlayerPosition.Forward,
        new DateOnly(1995, 5, 20), true, Now, null, [0, 0, 0, 0, 0, 0, 7, 209]);
}
