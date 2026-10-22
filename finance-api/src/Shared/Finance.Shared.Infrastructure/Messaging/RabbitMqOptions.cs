namespace Finance.Shared.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = "finance";

    public string Password { get; init; } = "finance";

    public string VirtualHost { get; init; } = "/";

    public string ExchangeName { get; init; } = "finance.events";

    public string DeadLetterExchangeName { get; init; } = "finance.dead-letter";

    public string DeadLetterQueueName { get; init; } = "finance.dead-letter";

    public ushort PrefetchCount { get; init; } = 10;

    public ushort ConsumerDispatchConcurrency { get; init; } = 4;

    public int ConnectionAttempts { get; init; } = 5;

    public string ClientProvidedName { get; init; } = "finance";
}
