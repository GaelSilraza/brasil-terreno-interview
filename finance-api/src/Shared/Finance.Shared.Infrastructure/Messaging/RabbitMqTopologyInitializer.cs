using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Finance.Shared.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer(
    RabbitMqTopologyDeclarer rabbitMqTopologyDeclarer,
    ILogger<RabbitMqTopologyInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await rabbitMqTopologyDeclarer.DeclareAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && !RabbitMqFailures.IsConfigurationError(exception))
        {
            logger.LogWarning(exception, "RabbitMQ is not available at startup. The topology is declared when the outbox opens its publishing channel.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
