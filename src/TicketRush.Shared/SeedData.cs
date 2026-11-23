namespace TicketRush.Shared;

public sealed record EventSeed(int Id, string Name, string Venue, DateTimeOffset StartsAt, int Capacity);

/// <summary>
/// Початковий каталог подій. Booking API заливає його в Redis, Order Worker — у PostgreSQL,
/// тому сервіси не залежать один від одного під час старту.
/// </summary>
public static class SeedData
{
    /// <summary>Подія для spike-тесту (flash sale): обмежена кількість місць.</summary>
    public const int FlashSaleEventId = 1;

    public static readonly IReadOnlyList<EventSeed> Events =
    [
        new(FlashSaleEventId, "Flash Sale: Headliner Live", "Palace of Sports, Kyiv", new DateTimeOffset(2026, 12, 20, 19, 0, 0, TimeSpan.FromHours(2)), 100),
        new(2, "Football: Final Match", "Olimpiyskiy Stadium, Kyiv", new DateTimeOffset(2026, 12, 27, 18, 0, 0, TimeSpan.FromHours(2)), 50_000),
        new(3, "Winter Jazz Festival", "Lviv Opera House", new DateTimeOffset(2027, 1, 15, 20, 0, 0, TimeSpan.FromHours(2)), 1_000),
    ];
}
