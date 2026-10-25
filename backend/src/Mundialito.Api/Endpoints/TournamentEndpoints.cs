using Mundialito.Api.Common;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.Tournament;
using Mundialito.Application.Tournament.Queries.GetStandings;
using Mundialito.Application.Tournament.Queries.GetTopScorers;

namespace Mundialito.Api.Endpoints;

/// <summary>Read-only tournament tables. Both are derived data: nothing here is ever written.</summary>
public sealed class TournamentEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Tournament");

        group.MapGet("/standings", GetStandings)
            .WithName("GetStandings")
            .WithSummary("Tabla de posiciones ordenada por puntos, diferencia de gol y goles a favor.")
            .Produces<StandingsResponse>();

        group.MapGet("/top-scorers", GetTopScorers)
            .WithName("GetTopScorers")
            .WithSummary("Tabla de goleadores paginada, opcionalmente filtrada por equipo.")
            .Produces<PagedResult<TopScorerResponse>>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> GetStandings(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetStandingsQuery(), cancellationToken);

        return result.ToOk();
    }

    private static async Task<IResult> GetTopScorers(
        Guid? teamId,
        int? pageNumber,
        int? pageSize,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetTopScorersQuery
        {
            TeamId = teamId,
            PageNumber = pageNumber ?? PagingDefaults.DefaultPageNumber,
            PageSize = pageSize ?? PagingDefaults.DefaultPageSize,
        };

        var result = await sender.Send(query, cancellationToken);

        return result.ToOk();
    }
}
