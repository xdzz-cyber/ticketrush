namespace TicketRush.BookingApi;

public sealed class BookingOptions
{
    /// <summary>Скільки триває тимчасова бронь до автоматичного звільнення.</summary>
    public int HoldTtlSeconds { get; set; } = 300;

    /// <summary>Скільки зберігати підтверджену бронь у Redis, щоб повторний confirm був ідемпотентним.</summary>
    public int ConfirmedHoldRetentionSeconds { get; set; } = 3600;

    /// <summary>Скільки пам'ятати результат за Idempotency-Key.</summary>
    public int IdempotencyTtlSeconds { get; set; } = 86400;
}

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Максимум одночасних публікацій (= відкритих каналів) на інстанс. Обмежує навантаження на брокер
    /// і не дає вийти за ліміт каналів RabbitMQ на з'єднання (channel_max = 2047) під піком.
    /// </summary>
    public int MaxPublishChannels { get; set; } = 64;
}
