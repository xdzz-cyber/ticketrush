using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TicketRush.Shared;
using TicketRush.Shared.Messages;

namespace TicketRush.BookingApi.Services;

public enum ReserveStatus { Reserved, Replayed, SoldOut, EventNotFound }

public sealed record ReserveResult(ReserveStatus Status, Guid ReservationId = default, DateTimeOffset ExpiresAt = default);

public enum ConfirmStatus { Confirmed, AlreadyConfirmed, NotFoundOrExpired }

public sealed record ConfirmResult(ConfirmStatus Status, BookingConfirmed? Message = null);

/// <summary>
/// Бронювання і підтвердження. Уся логіка узгодженості — в Lua-скриптах (Scripts/*.lua),
/// сервіс лише готує ключі/аргументи і розбирає відповідь. Стану не має — тому API масштабується горизонтально.
/// </summary>
public sealed class ReservationService(IConnectionMultiplexer redis, IOptions<BookingOptions> options, TimeProvider time)
{
    /// <summary>Ключ hold живе трохи довше за саму бронь, щоб confirm після закінчення часу коректно відповів "прострочено".</summary>
    private const int HoldKeyGraceSeconds = 60;

    private readonly BookingOptions _options = options.Value;

    public async Task<ReserveResult> ReserveAsync(int eventId, string userId, string idempotencyKey)
    {
        var reservationId = Guid.NewGuid();
        var expiresAtMs = time.GetUtcNow().AddSeconds(_options.HoldTtlSeconds).ToUnixTimeMilliseconds();
        var holdKeyTtlMs = (_options.HoldTtlSeconds + HoldKeyGraceSeconds) * 1000L;

        var result = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(
            LuaScripts.Reserve,
            [
                RedisKeys.Stock(eventId),
                RedisKeys.Hold(reservationId),
                RedisKeys.ExpiringHolds,
                RedisKeys.ReserveIdempotency(eventId, idempotencyKey),
            ],
            [
                reservationId.ToString("N"),
                eventId,
                userId,
                expiresAtMs,
                holdKeyTtlMs,
                _options.IdempotencyTtlSeconds,
                RedisKeys.HoldMember(eventId, reservationId),
            ]))!;

        return (int)result[0] switch
        {
            1 => Parse(ReserveStatus.Reserved, (string)result[1]!),
            2 => Parse(ReserveStatus.Replayed, (string)result[1]!),
            0 => new ReserveResult(ReserveStatus.SoldOut),
            _ => new ReserveResult(ReserveStatus.EventNotFound),
        };

        static ReserveResult Parse(ReserveStatus status, string value)
        {
            // Формат: "{reservationId:N}|{expiresAtMs}"
            var parts = value.Split('|');
            return new ReserveResult(
                status,
                Guid.ParseExact(parts[0], "N"),
                DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(parts[1])));
        }
    }

    public async Task<ConfirmResult> ConfirmAsync(Guid reservationId)
    {
        var result = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(
            LuaScripts.Confirm,
            [RedisKeys.Hold(reservationId), RedisKeys.ExpiringHolds],
            [
                time.GetUtcNow().ToUnixTimeMilliseconds(),
                reservationId.ToString("N"),
                _options.ConfirmedHoldRetentionSeconds * 1000L,
            ]))!;

        var code = (int)result[0];
        if (code == 0)
            return new ConfirmResult(ConfirmStatus.NotFoundOrExpired);

        var message = new BookingConfirmed(
            reservationId,
            EventId: int.Parse((string)result[1]!),
            UserId: (string)result[2]!,
            Quantity: 1,
            ConfirmedAt: DateTimeOffset.FromUnixTimeMilliseconds(long.Parse((string)result[3]!)));

        return new ConfirmResult(code == 1 ? ConfirmStatus.Confirmed : ConfirmStatus.AlreadyConfirmed, message);
    }

    /// <summary>Встановлює новий ліміт місць і скидає активні броні події. Для навантажувальних тестів.</summary>
    public async Task<bool> ResetEventAsync(int eventId, int capacity)
    {
        var removed = (long)await redis.GetDatabase().ScriptEvaluateAsync(
            LuaScripts.Reset,
            [RedisKeys.Stock(eventId), RedisKeys.Event(eventId), RedisKeys.ExpiringHolds],
            [capacity, $"{eventId}:"]);
        return removed >= 0;
    }
}
