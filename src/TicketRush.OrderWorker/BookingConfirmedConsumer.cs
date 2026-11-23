using Microsoft.Extensions.Options;
using TicketRush.Shared.Messages;

namespace TicketRush.OrderWorker;

/// <summary>
/// Споживає <see cref="BookingConfirmed"/> з черги <see cref="BookingConfirmed.QueueName"/>,
/// імітує оплату і зберігає замовлення в PostgreSQL.
/// Повідомлення: JSON, серіалізація через <see cref="MessageJson.Options"/>, MessageId = ReservationId.
/// </summary>
public sealed class BookingConfirmedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<BookingConfirmedConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = scopeFactory;
        _ = options;

        // TODO(Астахов): підключитися до RabbitMQ (RabbitMQ.Client 7, async API), оголосити durable-чергу BookingConfirmed.QueueName, BasicQos з prefetch і запустити AsyncEventingBasicConsumer з autoAck=false — consumer видно в RabbitMQ UI, повідомлення не губляться при падінні воркера.
        // TODO(Астахов): обробник: десеріалізувати BookingConfirmed (MessageJson.Options), імітувати оплату (Task.Delay кілька десятків мс), зберегти Order у PostgreSQL через scope з TicketRushDbContext, потім BasicAck — після spike-тесту кількість Orders у БД = кількість успішних confirm.
        // TODO(Астахов): ідемпотентність: повторна доставка того самого ReservationId не створює другий Order (унікальний індекс + обробка DbUpdateException як "вже оброблено" → BasicAck) — повторна публікація того самого повідомлення не змінює кількість рядків.
        // TODO(Астахов): помилки: тимчасова помилка БД → BasicNack(requeue: true), "битий" JSON → BasicNack(requeue: false) з логом — воркер не падає і не зациклюється на одному повідомленні.

        logger.LogWarning("BookingConfirmedConsumer is not implemented yet");
        return Task.CompletedTask;
    }
}
