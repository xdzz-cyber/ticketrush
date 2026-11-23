using TicketRush.BookingApi.Services;

namespace TicketRush.BookingApi.Endpoints;

public static class EventsEndpoints
{
    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        var events = app.MapGroup("/events");

        events.MapGet("/", async (CatalogService catalog) => Results.Ok(await catalog.GetAllAsync()));

        events.MapGet("/{eventId:int}", async (int eventId, CatalogService catalog) =>
            await catalog.GetAsync(eventId) is { } dto ? Results.Ok(dto) : Results.NotFound());

        return app;
    }
}
