using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Common;
using Mundialito.Api.Contracts.Players;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Players;
using Mundialito.Application.Players.Commands.DeletePlayer;
using Mundialito.Application.Players.Queries.GetPlayerById;

namespace Mundialito.Api.Endpoints;

/// <summary>
/// REST surface for players. Registration is nested under the team (POST /api/teams/{teamId}/players)
/// because a player only exists as a member of a roster; everything else addresses the player directly.
/// </summary>
public sealed class PlayerEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var teamPlayers = app.MapGroup("/api/teams/{teamId:guid}/players").WithTags("Players");

        teamPlayers.MapGet("/", GetTeamPlayers)
            .WithName("GetTeamPlayers")
            .WithSummary("Lista paginada de los jugadores de un equipo.")
            .Produces<PagedResult<PlayerResponse>>()
            .ProducesValidationProblem();

        teamPlayers.MapPost("/", CreatePlayer)
            .RequireIdempotencyKey()
            .WithName("CreatePlayer")
            .WithSummary("Registra un jugador en el equipo. Requiere la cabecera Idempotency-Key.")
            .Produces<PlayerResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var players = app.MapGroup("/api/players").WithTags("Players");

        players.MapGet("/", GetPlayers)
            .WithName("GetPlayers")
            .WithSummary("Lista paginada de jugadores de todos los equipos, con filtros y ordenamiento.")
            .Produces<PagedResult<PlayerResponse>>()
            .ProducesValidationProblem();

        players.MapGet("/{id:guid}", GetPlayerById)
            .WithName("GetPlayerById")
            .WithSummary("Obtiene un jugador por su identificador. Devuelve ETag.")
            .Produces<PlayerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        players.MapPut("/{id:guid}", UpdatePlayer)
            .WithName("UpdatePlayer")
            .WithSummary("Reemplaza por completo los datos editables de un jugador. Acepta If-Match.")
            .Produces<PlayerResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        players.MapPatch("/{id:guid}", PatchPlayer)
            .WithName("PatchPlayer")
            .WithSummary("Modifica solo los campos enviados de un jugador. Acepta If-Match.")
            .Produces<PlayerResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        players.MapDelete("/{id:guid}", DeletePlayer)
            .WithName("DeletePlayer")
            .WithSummary("Elimina un jugador sin goles registrados. Idempotente.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetTeamPlayers(
        Guid teamId,
        [AsParameters] GetPlayersRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToQuery(teamId), cancellationToken);

        return result.ToOk();
    }

    private static async Task<IResult> GetPlayers(
        [AsParameters] GetPlayersRequest request,
        Guid? teamId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToQuery(teamId), cancellationToken);

        return result.ToOk();
    }

    private static async Task<IResult> GetPlayerById(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPlayerByIdQuery(id), cancellationToken);

        return result.ToHttpResult(player => ETag.OkWithETag(player, player.RowVersion));
    }

    private static async Task<IResult> CreatePlayer(
        Guid teamId,
        [FromBody] CreatePlayerRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToCommand(teamId), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var created = await sender.Send(new GetPlayerByIdQuery(result.Value), cancellationToken);

        return created.ToHttpResult(player => Results.Created($"/api/players/{player.Id}", player));
    }

    private static async Task<IResult> UpdatePlayer(
        Guid id,
        [FromBody] UpdatePlayerRequest request,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ETag.TryGetExpectedVersion(httpRequest, out var expectedVersion, out var problem))
        {
            return problem!;
        }

        var result = await sender.Send(request.ToCommand(id, expectedVersion), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var updated = await sender.Send(new GetPlayerByIdQuery(id), cancellationToken);

        return updated.ToHttpResult(player => ETag.OkWithETag(player, player.RowVersion));
    }

    private static async Task<IResult> PatchPlayer(
        Guid id,
        [FromBody] PatchPlayerRequest request,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ETag.TryGetExpectedVersion(httpRequest, out var expectedVersion, out var problem))
        {
            return problem!;
        }

        var result = await sender.Send(request.ToCommand(id, expectedVersion), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var updated = await sender.Send(new GetPlayerByIdQuery(id), cancellationToken);

        return updated.ToHttpResult(player => ETag.OkWithETag(player, player.RowVersion));
    }

    private static async Task<IResult> DeletePlayer(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeletePlayerCommand(id), cancellationToken);

        return result.ToHttpResult(() => Results.NoContent());
    }
}
