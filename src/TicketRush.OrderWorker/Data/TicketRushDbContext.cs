using Microsoft.EntityFrameworkCore;

namespace TicketRush.OrderWorker.Data;

public sealed class EventEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Venue { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public int Capacity { get; set; }
}

public sealed class OrderEntity
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public int EventId { get; set; }
    public string UserId { get; set; } = "";
    public int Quantity { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset ConfirmedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}

public sealed class TicketRushDbContext(DbContextOptions<TicketRushDbContext> options) : DbContext(options)
{
    public DbSet<EventEntity> Events => Set<EventEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TODO(Астахов): налаштувати таблиці events/orders: унікальний індекс на Orders.ReservationId (основа ідемпотентності), FK Orders.EventId → Events.Id, обмеження довжини рядків — схема в PostgreSQL відповідає моделі, дубль ReservationId відхиляється БД.
        base.OnModelCreating(modelBuilder);
    }
}
