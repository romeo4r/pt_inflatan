using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Common.Pagination;

namespace Mundialito.Application.DomainEvents.Queries.GetDomainEvents;

/// <summary>Paged audit trail of domain events, newest first by default.</summary>
public sealed record GetDomainEventsQuery : IQuery<PagedResult<DomainEventLogResponse>>, IPagedQuery
{
    public static readonly string[] SortableFields = ["occurredAt", "eventType"];

    public string? EventType { get; init; }

    public Guid? AggregateId { get; init; }

    public string? CorrelationId { get; init; }

    public int PageNumber { get; init; } = PagingDefaults.DefaultPageNumber;

    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;

    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Desc;
}
