namespace TicketRush.Shared.Messages;

/// <summary>
/// Підтверджене бронювання. Публікує Booking API, споживає Order Worker.
/// Може бути доставлене повторно — обробник має бути ідемпотентним за <see cref="ReservationId"/>.
/// </summary>
public sealed record BookingConfirmed(
    Guid ReservationId,
    int EventId,
    string UserId,
    int Quantity,
    DateTimeOffset ConfirmedAt)
{
    /// <summary>Durable-черга в default exchange (routing key = назва черги).</summary>
    public const string QueueName = "booking.confirmed";
}
