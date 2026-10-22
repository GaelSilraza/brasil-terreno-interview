using Finance.Modules.Accounts.Infrastructure;
using Finance.Modules.Accounts.Infrastructure.Persistence.Seed;
using Finance.Modules.Transfers.Infrastructure;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Finance.Migrator;

public sealed class DatabaseInitializer(
    IServiceScopeFactory serviceScopeFactory,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger)
{
    private const int MaximumMigrationAttempts = 10;

    private static readonly TimeSpan MigrationRetryDelay = TimeSpan.FromSeconds(3);

    public static IServiceCollection Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFinancePersistence(configuration);
        services.AddAccountsModule(configuration);
        services.AddTransfersModule(configuration);
        services.AddSingleton<DatabaseInitializer>();
        return services;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();

        await MigrateWithRetriesAsync(dbContext, cancellationToken);

        if (configuration.GetValue<bool>("Seed:Enabled"))
        {
            var accountsSeeder = scope.ServiceProvider.GetRequiredService<AccountsSeeder>();
            await accountsSeeder.SeedAsync(cancellationToken);
        }
    }

    private async Task MigrateWithRetriesAsync(FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await dbContext.GetService<IHistoryRepository>().CreateIfNotExistsAsync(cancellationToken);
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied.");
                return;
            }
            catch (NpgsqlException exception) when (attempt < MaximumMigrationAttempts)
            {
                logger.LogWarning(exception, "Database not ready (attempt {Attempt}). Retrying.", attempt);
                await Task.Delay(MigrationRetryDelay, cancellationToken);
            }
        }
    }
}
