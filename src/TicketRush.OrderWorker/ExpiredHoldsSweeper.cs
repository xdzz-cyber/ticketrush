using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TicketRush.Shared;

namespace TicketRush.OrderWorker;

/// <summary>
/// Повертає в продаж місця з броней, які не підтвердили вчасно.
///
/// Схема даних (див. <see cref="RedisKeys"/>):
///   ZSET <see cref="RedisKeys.ExpiringHolds"/> — member "{eventId}:{reservationId:N}", score = expiresAt (unix ms);
///   STRING <see cref="RedisKeys.Stock"/> — залишок місць події.
/// Booking API при підтвердженні робить ZREM цього ж member. Хто перший зробив ZREM — той і вирішив долю броні,
/// тому місце повертається лише якщо ZREM у воркера повернув 1. Інакше бронь уже підтверджена — чіпати не можна.
/// </summary>
public sealed class ExpiredHoldsSweeper(
    IConnectionMultiplexer redis,
    IOptions<SweeperOptions> options,
    TimeProvider time,
    ILogger<ExpiredHoldsSweeper> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = redis;
        _ = options;
        _ = time;

        // TODO(Астахов): Lua-скрипт release.lua (embedded resource): ZRANGEBYSCORE holds:expiring -inf now LIMIT 0 BatchSize; для кожного member: якщо ZREM повернув 1 → INCR event:{eventId}:stock (eventId — частина member до ':'); повернути кількість звільнених — усе атомарно в одному скрипті, тест: бронь із TTL 5 c без confirm через ~10 c повертає +1 до available у GET /events/{id}.
        // TODO(Астахов): цикл кожні SweeperOptions.IntervalSeconds (PeriodicTimer) з логуванням кількості звільнених місць; помилки Redis логуються, цикл продовжується — воркер переживає рестарт Redis.

        logger.LogWarning("ExpiredHoldsSweeper is not implemented yet");
        return Task.CompletedTask;
    }
}
