using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TicketRush.Shared.Messages;

namespace TicketRush.BookingApi.Services;

/// <summary>
/// Публікує <see cref="BookingConfirmed"/> у RabbitMQ.
/// Одне з'єднання на інстанс + пул каналів (канал не призначений для паралельної публікації).
/// Publisher confirms увімкнені: PublishAsync завершується лише після того, як брокер прийняв повідомлення,
/// тому при успішній відповіді API повідомлення гарантовано в черзі (persistent, durable queue).
/// </summary>
public sealed class BookingPublisher(IOptions<RabbitMqOptions> options, ILogger<BookingPublisher> logger) : IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly ConcurrentBag<IChannel> _channels = [];
    private IConnection? _connection;

    public async Task PublishAsync(BookingConfirmed message, CancellationToken cancellationToken = default)
    {
        var channel = await RentChannelAsync(cancellationToken);
        var healthy = false;
        try
        {
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.ReservationId.ToString("N"),
                Type = nameof(BookingConfirmed),
            };
            var body = JsonSerializer.SerializeToUtf8Bytes(message, MessageJson.Options);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: BookingConfirmed.QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
            healthy = true;
        }
        finally
        {
            if (healthy && channel.IsOpen)
                _channels.Add(channel);
            else
                await channel.DisposeAsync();
        }
    }

    private async Task<IChannel> RentChannelAsync(CancellationToken cancellationToken)
    {
        while (_channels.TryTake(out var pooled))
        {
            if (pooled.IsOpen)
                return pooled;
            await pooled.DisposeAsync();
        }

        var connection = await GetConnectionAsync(cancellationToken);
        return await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            if (_connection is not null)
                await _connection.DisposeAsync();

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                ClientProvidedName = $"booking-api@{Environment.MachineName}",
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken);

            await using var setup = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await setup.QueueDeclareAsync(BookingConfirmed.QueueName, durable: true, exclusive: false, autoDelete: false,
                cancellationToken: cancellationToken);

            logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", _options.HostName, _options.Port);
            return _connection;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        while (_channels.TryTake(out var channel))
            await channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
