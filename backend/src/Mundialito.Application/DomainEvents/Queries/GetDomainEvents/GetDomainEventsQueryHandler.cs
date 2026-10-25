using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Common.Pagination;
using Mundialito.Domain.Common;

namespace Mundialito.Application.DomainEvents.Queries.GetDomainEvents;

internal sealed class GetDomainEventsQueryHandler(IDomainEventReadStore readStore)
    : IQueryHandler<GetDomainEventsQuery, PagedResult<DomainEventLogResponse>>
{
    public async Task<Result<PagedResult<DomainEventLogResponse>>> Handle(GetDomainEventsQuery query, CancellationToken cancellationToken) =>
        await readStore.GetPagedAsync(query, cancellationToken);
}
