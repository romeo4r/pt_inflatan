using Mundialito.Api.Common;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.DomainEvents;
using Mundialito.Application.DomainEvents.Queries.GetDomainEvents;

namespace Mundialito.Api.Endpoints;

/// <summary>Read-only audit trail: lets an operator follow what happened and under which request.</summary>
public sealed class DomainEventEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/domain-events", GetDomainEvents)
            .WithTags("Audit")
            .WithName("GetDomainEvents")
            .WithSummary("Bitácora paginada de eventos de dominio, filtrable por tipo, agregado y CorrelationId.")
            .Produces<PagedResult<DomainEventLogResponse>>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> GetDomainEvents(
        string? eventType,
        Guid? aggregateId,
        string? correlationId,
        int? pageNumber,
        int? pageSize,
        string? sortBy,
        string? sortDirection,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetDomainEventsQuery
        {
            EventType = eventType,
            AggregateId = aggregateId,
            CorrelationId = correlationId,
            PageNumber = pageNumber ?? PagingDefaults.DefaultPageNumber,
            PageSize = pageSize ?? PagingDefaults.DefaultPageSize,
            SortBy = sortBy,
            SortDirection = sortDirection is null ? SortDirection.Desc : PagingRequest.ParseSortDirection(sortDirection),
        };

        var result = await sender.Send(query, cancellationToken);

        return result.ToOk();
    }
}
