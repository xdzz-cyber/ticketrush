using System.Globalization;
using StackExchange.Redis;
using TicketRush.Shared;

namespace TicketRush.BookingApi.Services;

/// <summary>
/// На старті заливає каталог із <see cref="SeedData"/> у Redis, якщо його там ще немає.
/// Кілька інстансів можуть стартувати одночасно — це безпечно: залишок пишеться лише через SET NX,
/// тому рестарт API не "відновлює" вже продані місця.
/// </summary>
public sealed class CatalogInitializer(IConnectionMultiplexer redis, ILogger<CatalogInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await SeedAsync();
                logger.LogInformation("Catalog seeded: {Count} events", SeedData.Events.Count);
                return;
            }
            catch (RedisException ex) when (attempt < 30)
            {
                logger.LogWarning("Redis is not ready yet (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync()
    {
        var db = redis.GetDatabase();
        foreach (var e in SeedData.Events)
        {
            if (!await db.KeyExistsAsync(RedisKeys.Event(e.Id)))
            {
                await db.HashSetAsync(RedisKeys.Event(e.Id),
                [
                    new("name", e.Name),
                    new("venue", e.Venue),
                    new("startsAt", e.StartsAt.ToString("O", CultureInfo.InvariantCulture)),
                    new("capacity", e.Capacity),
                ]);
            }

            await db.StringSetAsync(RedisKeys.Stock(e.Id), e.Capacity, when: When.NotExists);
            await db.SetAddAsync(RedisKeys.EventIds, e.Id);
        }
    }
}
