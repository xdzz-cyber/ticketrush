using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StackExchange.Redis;
using Testcontainers.Redis;
using TicketRush.BookingApi;
using TicketRush.BookingApi.Services;
using TicketRush.Shared;

namespace TicketRush.BookingApi.Tests;

/// <summary>Справжній Redis у Docker (Testcontainers) — перевіряємо саме Lua-скрипти, без моків.</summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder().WithImage("redis:7.4-alpine").Build();

    public IConnectionMultiplexer Redis { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Redis = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
        await new CatalogInitializer(Redis, NullLogger<CatalogInitializer>.Instance).StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await Redis.DisposeAsync();
        await _container.DisposeAsync();
    }
}

public sealed class ReservationServiceTests(RedisFixture fixture) : IClassFixture<RedisFixture>
{
    private const int EventId = SeedData.FlashSaleEventId;

    private ReservationService CreateService(TimeProvider? time = null) =>
        new(fixture.Redis, Options.Create(new BookingOptions()), time ?? TimeProvider.System);

    private async Task<int> StockAsync() => (int)await fixture.Redis.GetDatabase().StringGetAsync(RedisKeys.Stock(EventId));

    [Fact]
    public async Task ParallelReservations_NeverSellMoreThanCapacity()
    {
        const int capacity = 100;
        const int buyers = 1_000;
        var service = CreateService();
        await service.ResetEventAsync(EventId, capacity);

        var results = await Task.WhenAll(Enumerable.Range(0, buyers).Select(i =>
            Task.Run(() => service.ReserveAsync(EventId, $"user-{i}", Guid.NewGuid().ToString()))));

        Assert.Equal(capacity, results.Count(r => r.Status == ReserveStatus.Reserved));
        Assert.Equal(buyers - capacity, results.Count(r => r.Status == ReserveStatus.SoldOut));
        Assert.Equal(0, await StockAsync());
    }

    [Fact]
    public async Task SameIdempotencyKey_ReservesOnlyOnce()
    {
        var service = CreateService();
        await service.ResetEventAsync(EventId, 10);
        var key = Guid.NewGuid().ToString();

        var first = await service.ReserveAsync(EventId, "user-1", key);
        var second = await service.ReserveAsync(EventId, "user-1", key);

        Assert.Equal(ReserveStatus.Reserved, first.Status);
        Assert.Equal(ReserveStatus.Replayed, second.Status);
        Assert.Equal(first.ReservationId, second.ReservationId);
        Assert.Equal(9, await StockAsync());
    }

    [Fact]
    public async Task Confirm_IsIdempotent_AndRemovesHoldFromExpiryQueue()
    {
        var service = CreateService();
        await service.ResetEventAsync(EventId, 10);
        var reservation = await service.ReserveAsync(EventId, "user-1", Guid.NewGuid().ToString());

        var first = await service.ConfirmAsync(reservation.ReservationId);
        var second = await service.ConfirmAsync(reservation.ReservationId);

        Assert.Equal(ConfirmStatus.Confirmed, first.Status);
        Assert.Equal(ConfirmStatus.AlreadyConfirmed, second.Status);
        Assert.Equal(first.Message, second.Message);
        Assert.Equal("user-1", first.Message!.UserId);
        var score = await fixture.Redis.GetDatabase().SortedSetScoreAsync(
            RedisKeys.ExpiringHolds, RedisKeys.HoldMember(EventId, reservation.ReservationId));
        Assert.Null(score); // Order Worker уже не поверне це місце в продаж
    }

    [Fact]
    public async Task Confirm_AfterHoldExpired_IsRejected()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(time);
        await service.ResetEventAsync(EventId, 10);
        var reservation = await service.ReserveAsync(EventId, "user-1", Guid.NewGuid().ToString());

        time.Advance(TimeSpan.FromSeconds(new BookingOptions().HoldTtlSeconds + 1));
        var result = await service.ConfirmAsync(reservation.ReservationId);

        Assert.Equal(ConfirmStatus.NotFoundOrExpired, result.Status);
    }

    [Fact]
    public async Task Reserve_UnknownEvent_ReturnsNotFound()
    {
        var result = await CreateService().ReserveAsync(9_999, "user-1", Guid.NewGuid().ToString());
        Assert.Equal(ReserveStatus.EventNotFound, result.Status);
    }
}
