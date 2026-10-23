using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Idempotency;
using Mundialito.Infrastructure.Persistence.Idempotency;

namespace Mundialito.UnitTests.Infrastructure.Persistence;

public sealed class IdempotencyStoreTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IdempotentResponse SampleResponse = new(201, "application/json", "{\"id\":1}", "/api/teams/1");

    private readonly SqliteContextFactory _factory = new();
    private readonly FixedClock _clock = new(Now);
    private readonly ServiceProvider _provider;
    private readonly IdempotencyStore _store;

    public IdempotencyStoreTests()
    {
        // The store opens its own scope per operation; give it a provider that hands out
        // contexts bound to the same in-memory SQLite database.
        var services = new ServiceCollection();
        services.AddScoped(_ => _factory.CreateContext());
        _provider = services.BuildServiceProvider();
        _store = new IdempotencyStore(_provider.GetRequiredService<IServiceScopeFactory>(), _clock);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task TryReserve_FirstTime_ShouldReserve()
    {
        var reservation = await _store.TryReserveAsync("k1", "hash", "POST", "/api/teams", CancellationToken.None);

        reservation.Outcome.ShouldBe(ReservationOutcome.Reserved);
        await using var context = _factory.CreateContext();
        var record = await context.IdempotencyKeys.SingleAsync();
        record.IsCompleted.ShouldBeFalse();
        record.ExpiresAtUtc.ShouldBe(Now.AddHours(24));
    }

    [Fact]
    public async Task TryReserve_WhilePending_ShouldReportInProgress()
    {
        await _store.TryReserveAsync("k1", "hash", "POST", "/api/teams", CancellationToken.None);

        var second = await _store.TryReserveAsync("k1", "hash", "POST", "/api/teams", CancellationToken.None);

        second.Outcome.ShouldBe(ReservationOutcome.InProgress);
    }

    [Fact]
    public async Task TryReserve_AfterComplete_ShouldReplayStoredResponse()
    {
        await _store.TryReserveAsync("k1", "hash", "POST", "/api/teams", CancellationToken.None);
        await _store.CompleteAsync("k1", SampleResponse, CancellationToken.None);

        var replay = await _store.TryReserveAsync("k1", "hash", "POST", "/api/teams", CancellationToken.None);

        replay.Outcome.ShouldBe(ReservationOutcome.Completed);
        replay.Response.ShouldBe(SampleResponse);
    }

    [Fact]
    public async Task TryReserve_WithDifferentPayload_ShouldReportMismatch()
    {
        await _store.TryReserveAsync("k1", "hash-a", "POST", "/api/teams", CancellationToken.None);
        await _store.CompleteAsync("k1", SampleResponse, CancellationToken.None);

        var other = await _store.TryReserveAsync("k1", "hash-b", "POST", "/api/teams", CancellationToken.None);

        other.Outcome.ShouldBe(ReservationOutcome.PayloadMismatch);
    }

    [Fact]
    public async Task Release_ShouldAllowReservingAgain()
    {
        await _store.TryReserveAsync("k1", "hash", "POST", "/api/teams", CancellationToken.None);

        await _store.ReleaseAsync("k1", CancellationToken.None);
        var again = await _store.TryReserveAsync("k1", "hash", "POST", "/api/teams", CancellationToken.None);

        again.Outcome.ShouldBe(ReservationOutcome.Reserved);
    }

    [Fact]
    public async Task TryReserve_WhenExpired_ShouldTreatKeyAsNew()
    {
        await _store.TryReserveAsync("k1", "hash", "POST", "/api/teams", CancellationToken.None);
        await _store.CompleteAsync("k1", SampleResponse, CancellationToken.None);

        _clock.UtcNow = Now.AddHours(25);
        var later = await _store.TryReserveAsync("k1", "other-hash", "POST", "/api/teams", CancellationToken.None);

        later.Outcome.ShouldBe(ReservationOutcome.Reserved);
    }

    [Fact]
    public async Task Complete_ForUnknownKey_ShouldBeNoOp()
    {
        await Should.NotThrowAsync(() => _store.CompleteAsync("missing", SampleResponse, CancellationToken.None));
    }
}
