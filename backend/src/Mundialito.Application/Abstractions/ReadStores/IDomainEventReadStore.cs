using Mundialito.Application.Common.Pagination;
using Mundialito.Application.DomainEvents;
using Mundialito.Application.DomainEvents.Queries.GetDomainEvents;

namespace Mundialito.Application.Abstractions.ReadStores;

/// <summary>Read side of the domain event audit trail (Dapper).</summary>
public interface IDomainEventReadStore
{
    Task<PagedResult<DomainEventLogResponse>> GetPagedAsync(GetDomainEventsQuery query, CancellationToken cancellationToken);
}
