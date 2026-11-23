namespace TicketRush.Shared;

/// <summary>
/// Схема ключів Redis, якою користуються обидва сервіси.
/// </summary>
public static class RedisKeys
{
    /// <summary>SET з id усіх подій каталогу.</summary>
    public const string EventIds = "events:ids";

    /// <summary>HASH з метаданими події: name, venue, startsAt, capacity.</summary>
    public static string Event(int eventId) => $"event:{eventId}";

    /// <summary>STRING (int) — скільки місць ще можна забронювати.</summary>
    public static string Stock(int eventId) => $"event:{eventId}:stock";

    /// <summary>HASH броні: eventId, userId, expiresAt (unix ms), status ("held" | "confirmed").</summary>
    public static string Hold(Guid reservationId) => $"hold:{reservationId:N}";

    /// <summary>
    /// ZSET активних (непідтверджених) броней. Member = <see cref="HoldMember"/>, score = expiresAt (unix ms).
    /// Правило: хто перший зробив ZREM члена — той і вирішив долю броні
    /// (confirm → підтверджено, Order Worker → місце повернуто в продаж через INCR stock).
    /// </summary>
    public const string ExpiringHolds = "holds:expiring";

    /// <summary>Member у <see cref="ExpiringHolds"/>: "{eventId}:{reservationId:N}".</summary>
    public static string HoldMember(int eventId, Guid reservationId) => $"{eventId}:{reservationId:N}";

    /// <summary>
    /// STRING з результатом запиту на бронювання для повторів з тим самим Idempotency-Key.
    /// Ключ прив'язаний до користувача, щоб чужий клієнт із тим самим ключем не отримав чужу бронь.
    /// userId не містить ':' (валідує Booking API), тому межа між userId і ключем однозначна.
    /// </summary>
    public static string ReserveIdempotency(int eventId, string userId, string idempotencyKey) =>
        $"idem:reserve:{eventId}:{userId}:{idempotencyKey}";
}
