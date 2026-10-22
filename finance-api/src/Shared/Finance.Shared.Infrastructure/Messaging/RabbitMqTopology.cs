using RabbitMQ.Client;

namespace Finance.Shared.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public static async Task DeclareExchangesAsync(IChannel channel, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: options.DeadLetterExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: options.DeadLetterQueueName,
            exchange: options.DeadLetterExchangeName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    public static async Task DeclareConsumerQueueAsync(
        IChannel channel,
        RabbitMqOptions options,
        string queueName,
        string bindingKey,
        CancellationToken cancellationToken)
    {
        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = options.DeadLetterExchangeName,
        };

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: options.ExchangeName,
            routingKey: bindingKey,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
