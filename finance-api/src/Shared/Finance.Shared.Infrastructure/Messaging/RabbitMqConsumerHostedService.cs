using System.Text.Json;
using Finance.Shared.Application.Messaging;
using Finance.Shared.Infrastructure.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Finance.Shared.Infrastructure.Messaging;

public sealed class RabbitMqConsumerHostedService<TMessage, TProcessor>(
    RabbitMqConsumerQueue consumerQueue,
    RabbitMqConnectionProvider rabbitMqConnectionProvider,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    ILogger<RabbitMqConsumerHostedService<TMessage, TProcessor>> logger) : BackgroundService
    where TMessage : class
    where TProcessor : class, IMessageProcessor<TMessage>
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilStoppedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Consumer of queue {QueueName} stopped unexpectedly. Restarting.", consumerQueue.QueueName);
                await Task.Delay(RestartDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeUntilStoppedAsync(CancellationToken stoppingToken)
    {
        var options = rabbitMqOptions.Value;
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await RabbitMqTopology.DeclareExchangesAsync(channel, options, stoppingToken);
        await RabbitMqTopology.DeclareConsumerQueueAsync(channel, options, consumerQueue.QueueName, consumerQueue.BindingKey, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: options.PrefetchCount, global: false, cancellationToken: stoppingToken);

        var consumerStopped = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, deliverEventArgs) => HandleDeliveryAsync(channel, deliverEventArgs, stoppingToken);
        consumer.ShutdownAsync += (_, shutdownEventArgs) =>
        {
            consumerStopped.TrySetResult($"the channel was closed ({shutdownEventArgs.ReplyCode} {shutdownEventArgs.ReplyText})");
            return Task.CompletedTask;
        };
        consumer.UnregisteredAsync += (_, _) =>
        {
            consumerStopped.TrySetResult("the broker cancelled the consumer");
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queue: consumerQueue.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        logger.LogInformation("Consuming queue {QueueName} bound to {BindingKey}.", consumerQueue.QueueName, consumerQueue.BindingKey);

        var stopReason = await consumerStopped.Task.WaitAsync(stoppingToken);
        throw new InvalidOperationException($"Consuming queue {consumerQueue.QueueName} stopped because {stopReason}.");
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs deliverEventArgs, CancellationToken stoppingToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<TMessage>(deliverEventArgs.Body.Span, FinanceJsonSerializerOptions.Default)
                ?? throw new InvalidOperationException("The message body is empty.");

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<TProcessor>();
            await processor.ProcessAsync(message, stoppingToken);

            await channel.BasicAckAsync(deliverEventArgs.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await channel.BasicNackAsync(deliverEventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Message {MessageId} from queue {QueueName} failed and was sent to the dead-letter exchange.",
                deliverEventArgs.BasicProperties.MessageId,
                consumerQueue.QueueName);
            await channel.BasicNackAsync(deliverEventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken: CancellationToken.None);
        }
    }
}
