using System.Net;
using System.Text.Json;
using Mundialito.Application.Common.Pagination;
using Mundialito.Application.DomainEvents;
using Mundialito.Application.DomainEvents.Queries.GetDomainEvents;
using Mundialito.Domain.Common;
using NSubstitute;
using SortDirection = Mundialito.Application.Common.Pagination.SortDirection;

namespace Mundialito.UnitTests.Api;

public sealed class DomainEventEndpointsTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly ApiTestFactory _factory = new();
    private readonly HttpClient _client;

    public DomainEventEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetDomainEvents_ShouldBindFiltersAndDefaultToNewestFirst()
    {
        GetDomainEventsQuery? captured = null;
        var aggregateId = Guid.CreateVersion7();
        var entry = new DomainEventLogResponse(
            Guid.CreateVersion7(), "TeamCreatedDomainEvent", aggregateId, "{\"code\":\"FIN\"}", "corr-1", "trace-1", Now, Now);
        _factory.Sender
            .Send(Arg.Do<GetDomainEventsQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PagedResult<DomainEventLogResponse>([entry], 1, 10, 1)));

        var response = await _client.GetAsync($"/api/domain-events?eventType=TeamCreatedDomainEvent&aggregateId={aggregateId}&correlationId=corr-1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.EventType.ShouldBe("TeamCreatedDomainEvent");
        captured.AggregateId.ShouldBe(aggregateId);
        captured.CorrelationId.ShouldBe("corr-1");
        captured.SortDirection.ShouldBe(SortDirection.Desc);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = body.RootElement.GetProperty("data")[0];
        first.GetProperty("eventType").GetString().ShouldBe("TeamCreatedDomainEvent");
        first.GetProperty("payload").GetString().ShouldBe("{\"code\":\"FIN\"}");
        first.GetProperty("correlationId").GetString().ShouldBe("corr-1");
    }

    [Fact]
    public async Task GetDomainEvents_WithExplicitAscending_ShouldHonourIt()
    {
        GetDomainEventsQuery? captured = null;
        _factory.Sender
            .Send(Arg.Do<GetDomainEventsQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(Result.Success(PagedResult<DomainEventLogResponse>.Empty(1, 10)));

        await _client.GetAsync("/api/domain-events?sortDirection=asc");

        captured.ShouldNotBeNull().SortDirection.ShouldBe(SortDirection.Asc);
    }
}
