using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Common;
using Mundialito.Api.Contracts.Teams;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Teams;
using Mundialito.Application.Teams.Commands.DeleteTeam;
using Mundialito.Application.Teams.Queries.GetTeamById;

namespace Mundialito.Api.Endpoints;

/// <summary>
/// REST surface for teams. Each verb keeps its HTTP semantics: GET reads, POST creates (201 +
/// Location), PUT replaces, PATCH changes part, DELETE removes and is idempotent (204 even when
/// the team is already gone). Endpoints only translate HTTP to commands/queries and back.
/// </summary>
public sealed class TeamEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams").WithTags("Teams");

        group.MapGet("/", GetTeams)
            .WithName("GetTeams")
            .WithSummary("Lista paginada de equipos con búsqueda, filtro por estado y ordenamiento.")
            .Produces<PagedResult<TeamResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetTeamById)
            .WithName("GetTeamById")
            .WithSummary("Obtiene un equipo por su identificador.")
            .Produces<TeamResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateTeam)
            .WithName("CreateTeam")
            .WithSummary("Crea un equipo.")
            .Produces<TeamResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateTeam)
            .WithName("UpdateTeam")
            .WithSummary("Reemplaza por completo los datos editables de un equipo.")
            .Produces<TeamResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}", PatchTeam)
            .WithName("PatchTeam")
            .WithSummary("Modifica solo los campos enviados de un equipo.")
            .Produces<TeamResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteTeam)
            .WithName("DeleteTeam")
            .WithSummary("Elimina un equipo y su plantilla. Idempotente.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetTeams(
        [AsParameters] GetTeamsRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToQuery(), cancellationToken);

        return result.ToOk();
    }

    private static async Task<IResult> GetTeamById(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTeamByIdQuery(id), cancellationToken);

        return result.ToOk();
    }

    private static async Task<IResult> CreateTeam(
        [FromBody] CreateTeamRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToCommand(), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // The command returns the id; the representation comes from the read side (CQRS).
        var created = await sender.Send(new GetTeamByIdQuery(result.Value), cancellationToken);

        return created.ToHttpResult(team => Results.Created($"/api/teams/{team.Id}", team));
    }

    private static async Task<IResult> UpdateTeam(
        Guid id,
        [FromBody] UpdateTeamRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var updated = await sender.Send(new GetTeamByIdQuery(id), cancellationToken);

        return updated.ToOk();
    }

    private static async Task<IResult> PatchTeam(
        Guid id,
        [FromBody] PatchTeamRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var updated = await sender.Send(new GetTeamByIdQuery(id), cancellationToken);

        return updated.ToOk();
    }

    private static async Task<IResult> DeleteTeam(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteTeamCommand(id), cancellationToken);

        return result.ToHttpResult(() => Results.NoContent());
    }
}
