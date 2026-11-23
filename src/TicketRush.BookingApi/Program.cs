using StackExchange.Redis;
using TicketRush.BookingApi;
using TicketRush.BookingApi.Endpoints;
using TicketRush.BookingApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection("Booking"));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]
        ?? throw new InvalidOperationException("Redis:ConnectionString is not configured")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddSingleton<ReservationService>();
builder.Services.AddSingleton<BookingPublisher>();
builder.Services.AddHostedService<CatalogInitializer>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Показує, який інстанс обробив запит, — видно балансування Nginx і в k6, і в curl.
app.Use((context, next) =>
{
    context.Response.Headers["X-Instance"] = Environment.MachineName;
    return next(context);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", instance = Environment.MachineName }));
app.MapEvents();
app.MapReservations();
if (app.Configuration.GetValue<bool>("Admin:Enabled"))
    app.MapAdmin();

app.Run();

public partial class Program;
