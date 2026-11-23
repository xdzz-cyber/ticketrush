using TicketRush.BookingApi.Services;

namespace TicketRush.BookingApi.Endpoints;

public sealed record ResetEventRequest(int Capacity);

/// <summary>Службові ендпоінти для навантажувальних тестів. Підключаються лише при Admin:Enabled=true.</summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/events/{eventId:int}/reset", async (int eventId, ResetEventRequest request, ReservationService reservations) =>
        {
            if (request.Capacity < 0)
                return Results.Problem("'capacity' must be >= 0.", statusCode: StatusCodes.Status400BadRequest);

            return await reservations.ResetEventAsync(eventId, request.Capacity)
                ? Results.NoContent()
                : Results.NotFound();
        });
        return app;
    }
}
