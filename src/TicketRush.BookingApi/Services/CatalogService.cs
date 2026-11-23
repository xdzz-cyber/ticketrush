using System.Globalization;
using StackExchange.Redis;
using TicketRush.Shared;

namespace TicketRush.BookingApi.Services;

public sealed record EventDto(int Id, string Name, string Venue, DateTimeOffset StartsAt, int Capacity, int Available);

/// <summary>
/// Каталог подій читається з Redis: метадані (HASH event:{id}) + живий залишок (event:{id}:stock).
/// До PostgreSQL на критичному шляху запиту не ходимо.
/// </summary>
public sealed class CatalogService(IConnectionMultiplexer redis)
{
    public async Task<IReadOnlyList<EventDto>> GetAllAsync()
    {
        var ids = await redis.GetDatabase().SetMembersAsync(RedisKeys.EventIds);
        // Запити йдуть паралельно; StackExchange.Redis сам конвеєризує їх в одне з'єднання.
        var events = await Task.WhenAll(ids.Select(id => GetAsync((int)id)));
        return events.OfType<EventDto>().OrderBy(e => e.Id).ToList();
    }

    public async Task<EventDto?> GetAsync(int eventId)
    {
        var db = redis.GetDatabase();
        var hashTask = db.HashGetAllAsync(RedisKeys.Event(eventId));
        var stockTask = db.StringGetAsync(RedisKeys.Stock(eventId));
        await Task.WhenAll(hashTask, stockTask);

        var hash = hashTask.Result.ToStringDictionary();
        if (hash.Count == 0)
            return null;

        return new EventDto(
            eventId,
            hash["name"],
            hash["venue"],
            DateTimeOffset.Parse(hash["startsAt"], CultureInfo.InvariantCulture),
            int.Parse(hash["capacity"]),
            // Не обрізаємо до 0: від'ємне значення означало б oversell, і k6 має це побачити.
            Available: (int)stockTask.Result);
    }
}
