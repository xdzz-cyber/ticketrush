using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using TicketRush.BookingApi.Services;

namespace TicketRush.BookingApi.Endpoints;

public sealed record ReserveRequest(string? UserId);

public sealed record ReservationResponse(Guid ReservationId, int EventId, DateTimeOffset ExpiresAt);

public sealed record ConfirmResponse(Guid ReservationId, string Status);

public static partial class ReservationsEndpoints
{
    private const int MaxIdempotencyKeyLength = 100;

    /// <summary>1..64 символи без ':' — userId входить у ключі Redis.</summary>
    [GeneratedRegex("^[A-Za-z0-9._@-]{1,64}$")]
    private static partial Regex UserIdPattern();

    public static IEndpointRouteBuilder MapReservations(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events/{eventId:int}/reservations", ReserveAsync);
        app.MapPost("/reservations/{reservationId:guid}/confirm", ConfirmAsync);
        return app;
    }

    private static async Task<IResult> ReserveAsync(
        int eventId,
        ReserveRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ReservationService reservations)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxIdempotencyKeyLength)
            return Results.Problem($"Header 'Idempotency-Key' is required (1..{MaxIdempotencyKeyLength} chars).",
                statusCode: StatusCodes.Status400BadRequest);
        if (request.UserId is null || !UserIdPattern().IsMatch(request.UserId))
            return Results.Problem("'userId' is required: 1..64 chars of letters, digits, '.', '_', '@', '-'.",
                statusCode: StatusCodes.Status400BadRequest);

        var result = await reservations.ReserveAsync(eventId, request.UserId, idempotencyKey);
        var body = new ReservationResponse(result.ReservationId, eventId, result.ExpiresAt);

        return result.Status switch
        {
            ReserveStatus.Reserved => Results.Created($"/reservations/{result.ReservationId}", body),
            ReserveStatus.Replayed => Results.Ok(body),
            ReserveStatus.SoldOut => Results.Problem("No seats left.", statusCode: StatusCodes.Status409Conflict),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> ConfirmAsync(
        Guid reservationId,
        ReservationService reservations,
        BookingPublisher publisher,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var result = await reservations.ConfirmAsync(reservationId);
        if (result.Status == ConfirmStatus.NotFoundOrExpired)
            return Results.Problem("Reservation not found or expired.", statusCode: StatusCodes.Status410Gone);

        // Публікуємо і при першому, і при повторному confirm: якщо минулого разу публікація впала
        // після запису в Redis, повтор клієнта "дошле" повідомлення. Order Worker ідемпотентний.
        try
        {
            await publisher.PublishAsync(result.Message!, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(typeof(ReservationsEndpoints))
                .LogError(ex, "Failed to publish BookingConfirmed for {ReservationId}", reservationId);
            return Results.Problem("Order queue is unavailable, retry the request.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Accepted(value: new ConfirmResponse(reservationId, "confirmed"));
    }
}
