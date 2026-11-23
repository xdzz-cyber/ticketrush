using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TicketRush.OrderWorker;
using TicketRush.OrderWorker.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<SweeperOptions>(builder.Configuration.GetSection("Sweeper"));

builder.Services.AddDbContext<TicketRushDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]
        ?? throw new InvalidOperationException("Redis:ConnectionString is not configured")));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHostedService<BookingConfirmedConsumer>();
builder.Services.AddHostedService<ExpiredHoldsSweeper>();

var host = builder.Build();

// TODO(Астахов): створити схему БД і залити події з TicketRush.Shared.SeedData (EnsureCreated або міграції EF Core, з повтором, поки PostgreSQL стартує) — після `docker compose up` таблиці Events (3 рядки) і Orders існують.

host.Run();
