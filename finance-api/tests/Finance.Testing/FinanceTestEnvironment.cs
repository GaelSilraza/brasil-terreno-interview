using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Finance.Testing;

public sealed class FinanceTestEnvironment : IAsyncDisposable
{
    private readonly string connectionString;

    public FinanceTestEnvironment(
        string connectionString,
        IReadOnlyDictionary<string, string?> configurationValues,
        Action<IServiceCollection, IConfiguration> registerServices)
    {
        this.connectionString = connectionString;
        TimeProvider = new FakeTimeProvider(TestClock.DaytimeInSaoPaulo);
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddSingleton<TimeProvider>(TimeProvider);
        services.AddFinancePersistence(Configuration);
        registerServices(services, Configuration);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public FakeTimeProvider TimeProvider { get; }

    public IConfiguration Configuration { get; }

    public ServiceProvider Services { get; }

    public AsyncServiceScope CreateScope()
    {
        return Services.CreateAsyncScope();
    }

    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken)
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await dbContext.GetService<IHistoryRepository>().CreateIfNotExistsAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public async Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken)
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableNames = new List<string>();
        await using (var listTablesCommand = new NpgsqlCommand(
            "SELECT format('%I.%I', schemaname, tablename) FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__ef_migrations_history'",
            connection))
        await using (var reader = await listTablesCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        if (tableNames.Count == 0)
        {
            return;
        }

        await using var truncateCommand = new NpgsqlCommand($"TRUNCATE TABLE {string.Join(", ", tableNames)} CASCADE", connection);
        await truncateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
    }
}
