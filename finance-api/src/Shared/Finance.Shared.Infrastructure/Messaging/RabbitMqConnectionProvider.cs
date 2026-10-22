using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Finance.Shared.Infrastructure.Messaging;

public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> rabbitMqOptions,
    ILogger<RabbitMqConnectionProvider> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private IConnection? connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is { IsOpen: true })
        {
            return connection;
        }

        await connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (connection is { IsOpen: true })
            {
                return connection;
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
                connection = null;
            }

            connection = await CreateConnectionWithRetriesAsync(cancellationToken);
            return connection;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        connectionLock.Dispose();
    }

    private async Task<IConnection> CreateConnectionWithRetriesAsync(CancellationToken cancellationToken)
    {
        var options = rabbitMqOptions.Value;
        var connectionFactory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ConsumerDispatchConcurrency = options.ConsumerDispatchConcurrency,
            ClientProvidedName = options.ClientProvidedName,
        };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await connectionFactory.CreateConnectionAsync(cancellationToken);
            }
            catch (BrokerUnreachableException exception) when (attempt < options.ConnectionAttempts && !RabbitMqFailures.IsConfigurationError(exception))
            {
                var retryDelay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning(
                    exception,
                    "RabbitMQ is unreachable. Retrying in {RetryDelaySeconds} seconds (attempt {Attempt} of {ConnectionAttempts}).",
                    retryDelay.TotalSeconds,
                    attempt,
                    options.ConnectionAttempts);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }
}
