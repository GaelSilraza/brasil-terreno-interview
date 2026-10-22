using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Finance.Testing;

public abstract class FinanceTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgreSqlContainer = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("finance")
        .WithUsername("finance")
        .WithPassword("finance")
        .Build();

    private RabbitMqContainer? rabbitMqContainer;

    public string ConnectionString => postgreSqlContainer.GetConnectionString();

    protected virtual bool UsesRabbitMq => false;

    protected virtual bool UsesMigrations => false;

    public async ValueTask InitializeAsync()
    {
        await postgreSqlContainer.StartAsync();

        if (UsesRabbitMq)
        {
            rabbitMqContainer = new RabbitMqBuilder("rabbitmq:4-management-alpine")
                .WithUsername("finance")
                .WithPassword("finance")
                .Build();
            await rabbitMqContainer.StartAsync();
        }

        await PrepareDatabaseAsync(CancellationToken.None);
    }

    public FinanceTestEnvironment CreateEnvironment(IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        var configurationValues = CreateConfigurationValues();

        foreach (var configurationOverride in configurationOverrides ?? new Dictionary<string, string?>())
        {
            configurationValues[configurationOverride.Key] = configurationOverride.Value;
        }

        return new FinanceTestEnvironment(ConnectionString, configurationValues, RegisterServices);
    }

    public Dictionary<string, string?> CreateConfigurationValues()
    {
        var configurationValues = TestConfiguration.CreateDefault(ConnectionString);

        if (rabbitMqContainer is not null)
        {
            configurationValues["RabbitMq:HostName"] = rabbitMqContainer.Hostname;
            configurationValues["RabbitMq:Port"] = rabbitMqContainer.GetMappedPublicPort(5672).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return configurationValues;
    }

    public async Task CloseRabbitMqConnectionsAsync(CancellationToken cancellationToken)
    {
        var container = rabbitMqContainer ?? throw new InvalidOperationException("This fixture does not start RabbitMQ.");
        var result = await container.ExecAsync(["rabbitmqctl", "close_all_connections", "closed by test"], cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Closing the RabbitMQ connections failed: {result.Stderr}");
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        await postgreSqlContainer.DisposeAsync();

        if (rabbitMqContainer is not null)
        {
            await rabbitMqContainer.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    protected abstract void RegisterServices(IServiceCollection services, IConfiguration configuration);

    protected virtual async Task PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var environment = CreateEnvironment();

        if (UsesMigrations)
        {
            await environment.ApplyMigrationsAsync(cancellationToken);
            return;
        }

        await environment.EnsureDatabaseCreatedAsync(cancellationToken);
    }
}
