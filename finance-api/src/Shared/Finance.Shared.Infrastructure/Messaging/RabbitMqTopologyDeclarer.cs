using Microsoft.Extensions.Options;

namespace Finance.Shared.Infrastructure.Messaging;

public sealed class RabbitMqTopologyDeclarer(
    RabbitMqConnectionProvider rabbitMqConnectionProvider,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IEnumerable<RabbitMqConsumerQueue> consumerQueues)
{
    public async Task DeclareAsync(CancellationToken cancellationToken)
    {
        var options = rabbitMqOptions.Value;
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqTopology.DeclareExchangesAsync(channel, options, cancellationToken);

        foreach (var consumerQueue in consumerQueues)
        {
            await RabbitMqTopology.DeclareConsumerQueueAsync(channel, options, consumerQueue.QueueName, consumerQueue.BindingKey, cancellationToken);
        }
    }
}
