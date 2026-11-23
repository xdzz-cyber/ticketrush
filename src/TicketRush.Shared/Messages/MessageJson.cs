using System.Text.Json;

namespace TicketRush.Shared.Messages;

/// <summary>Єдині налаштування JSON для повідомлень у RabbitMQ (camelCase).</summary>
public static class MessageJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
