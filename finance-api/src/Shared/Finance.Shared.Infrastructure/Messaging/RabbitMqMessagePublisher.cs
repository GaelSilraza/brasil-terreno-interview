using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Finance.Shared.Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher(
    RabbitMqConnectionProvider rabbitMqConnectionProvider,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    RabbitMqTopologyDeclarer rabbitMqTopologyDeclarer) : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim channelLock = new(1, 1);
    private IChannel? publishingChannel;

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        await channelLock.WaitAsync(cancellationToken);

        try
        {
            await GetPublishingChannelAsync(cancellationToken);
        }
        finally
        {
            channelLock.Release();
        }
    }

    public async Task PublishAsync(OutgoingMessage outgoingMessage, CancellationToken cancellationToken)
    {
        await channelLock.WaitAsync(cancellationToken);

        try
        {
            var channel = await GetPublishingChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                MessageId = outgoingMessage.MessageId.ToString(),
                Type = outgoingMessage.Type,
                ContentType = "application/json",
                Persistent = true,
            };

            await channel.BasicPublishAsync(
                exchange: rabbitMqOptions.Value.ExchangeName,
                routingKey: outgoingMessage.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outgoingMessage.Content),
                cancellationToken: cancellationToken);
        }
        catch (PublishException exception) when (exception.IsReturn)
        {
            await DiscardPublishingChannelAsync();
            throw new MessageRejectedException($"The broker could not route the message with routing key '{outgoingMessage.RoutingKey}' to any queue.", exception);
        }
        catch (PublishException exception)
        {
            throw new MessageRejectedException("The broker did not confirm the message.", exception);
        }
        finally
        {
            channelLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (publishingChannel is not null)
        {
            await publishingChannel.DisposeAsync();
        }

        channelLock.Dispose();
    }

    private async Task<IChannel> GetPublishingChannelAsync(CancellationToken cancellationToken)
    {
        if (publishingChannel is { IsOpen: true })
        {
            return publishingChannel;
        }

        await DiscardPublishingChannelAsync();
        await rabbitMqTopologyDeclarer.DeclareAsync(cancellationToken);
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        publishingChannel = await connection.CreateChannelAsync(channelOptions, cancellationToken);
        return publishingChannel;
    }

    private async Task DiscardPublishingChannelAsync()
    {
        if (publishingChannel is not null)
        {
            await publishingChannel.DisposeAsync();
            publishingChannel = null;
        }
    }
}
