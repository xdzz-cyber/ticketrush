namespace TicketRush.OrderWorker;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public sealed class SweeperOptions
{
    /// <summary>Як часто шукати прострочені броні.</summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>Скільки броней обробляти за один прохід.</summary>
    public int BatchSize { get; set; } = 500;
}
