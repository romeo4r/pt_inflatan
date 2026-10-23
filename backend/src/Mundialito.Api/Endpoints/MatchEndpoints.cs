using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Common;
using Mundialito.Api.Contracts.Matches;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Matches;
using Mundialito.Application.Matches.Commands.CancelMatch;
using Mundialito.Application.Matches.Commands.DeleteMatch;
using Mundialito.Application.Matches.Queries.GetMatchById;

namespace Mundialito.Api.Endpoints;

/// <summary>
/// REST surface for fixtures. Besides the usual verbs, "cancel" is exposed as an action
/// (POST /api/matches/{id}/cancel) because it is a state transition, not a field edit.
/// Result registration lives in its own endpoint (next phase).
/// </summary>
public sealed class MatchEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/matches").WithTags("Matches");

        group.MapGet("/", GetMatches)
            .WithName("GetMatches")
            .WithSummary("Lista paginada de partidos con filtros por fecha, equipo, estado y jornada.")
            .Produces<PagedResult<MatchResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetMatchById)
            .WithName("GetMatchById")
            .WithSummary("Obtiene un partido con sus goles. Devuelve ETag.")
            .Produces<MatchDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", ScheduleMatch)
            .RequireIdempotencyKey()
            .WithName("ScheduleMatch")
            .WithSummary("Programa un partido entre dos equipos. Requiere la cabecera Idempotency-Key.")
            .Produces<MatchDetailResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", RescheduleMatch)
            .WithName("RescheduleMatch")
            .WithSummary("Reemplaza jornada, fecha y sede de un partido programado. Acepta If-Match.")
            .Produces<MatchDetailResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}", PatchMatch)
            .WithName("PatchMatch")
            .WithSummary("Modifica solo los campos enviados de un partido programado. Acepta If-Match.")
            .Produces<MatchDetailResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", CancelMatch)
            .WithName("CancelMatch")
            .WithSummary("Cancela un partido programado. Repetirlo no tiene efecto. Acepta If-Match.")
            .Produces<MatchDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/result", RegisterResult)
            .RequireIdempotencyKey()
            .WithName("RegisterMatchResult")
            .WithSummary("Registra el marcador final y los goleadores de un partido programado. Requiere Idempotency-Key; acepta If-Match.")
            .Produces<MatchDetailResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteMatch)
            .WithName("DeleteMatch")
            .WithSummary("Elimina un partido no jugado. Idempotente.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetMatches(
        [AsParameters] GetMatchesRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToQuery(), cancellationToken);

        return result.ToOk();
    }

    private static async Task<IResult> GetMatchById(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMatchByIdQuery(id), cancellationToken);

        return result.ToHttpResult(detail => ETag.OkWithETag(detail, detail.Match.RowVersion));
    }

    private static async Task<IResult> ScheduleMatch(
        [FromBody] ScheduleMatchRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToCommand(), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var created = await sender.Send(new GetMatchByIdQuery(result.Value), cancellationToken);

        return created.ToHttpResult(detail => Results.Created($"/api/matches/{detail.Match.Id}", detail));
    }

    private static async Task<IResult> RescheduleMatch(
        Guid id,
        [FromBody] RescheduleMatchRequest request,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ETag.TryGetExpectedVersion(httpRequest, out var expectedVersion, out var problem))
        {
            return problem!;
        }

        var result = await sender.Send(request.ToCommand(id, expectedVersion), cancellationToken);

        return await RespondWithMatch(id, result, sender, cancellationToken);
    }

    private static async Task<IResult> PatchMatch(
        Guid id,
        [FromBody] PatchMatchRequest request,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ETag.TryGetExpectedVersion(httpRequest, out var expectedVersion, out var problem))
        {
            return problem!;
        }

        var result = await sender.Send(request.ToCommand(id, expectedVersion), cancellationToken);

        return await RespondWithMatch(id, result, sender, cancellationToken);
    }

    private static async Task<IResult> CancelMatch(
        Guid id,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ETag.TryGetExpectedVersion(httpRequest, out var expectedVersion, out var problem))
        {
            return problem!;
        }

        var result = await sender.Send(new CancelMatchCommand(id, expectedVersion), cancellationToken);

        return await RespondWithMatch(id, result, sender, cancellationToken);
    }

    private static async Task<IResult> RegisterResult(
        Guid id,
        [FromBody] RegisterMatchResultRequest request,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ETag.TryGetExpectedVersion(httpRequest, out var expectedVersion, out var problem))
        {
            return problem!;
        }

        var result = await sender.Send(request.ToCommand(id, expectedVersion), cancellationToken);

        return await RespondWithMatch(id, result, sender, cancellationToken);
    }

    private static async Task<IResult> DeleteMatch(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteMatchCommand(id), cancellationToken);

        return result.ToHttpResult(() => Results.NoContent());
    }

    /// <summary>After a successful command, returns the fresh representation from the read side.</summary>
    private static async Task<IResult> RespondWithMatch(
        Guid id,
        Domain.Common.Result commandResult,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (commandResult.IsFailure)
        {
            return commandResult.Error.ToProblem();
        }

        var updated = await sender.Send(new GetMatchByIdQuery(id), cancellationToken);

        return updated.ToHttpResult(detail => ETag.OkWithETag(detail, detail.Match.RowVersion));
    }
}
