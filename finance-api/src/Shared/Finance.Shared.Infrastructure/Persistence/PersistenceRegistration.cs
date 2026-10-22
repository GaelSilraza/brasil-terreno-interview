using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Messaging;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Infrastructure.DomainEvents;
using Finance.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Finance.Shared.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    public const string ConnectionStringName = "Finance";

    public const string MigrationsAssemblyName = "Finance.Migrator";

    public static IServiceCollection AddFinancePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<FinanceDbContext>(options => ConfigureDbContext(options, connectionString));
        services.AddScoped<IUnitOfWork, EntityFrameworkUnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IOutbox, EntityFrameworkOutbox>();
        services.AddScoped<IAdvisoryLock, PostgresAdvisoryLock>();
        return services;
    }

    public static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        options
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions
                .MigrationsAssembly(MigrationsAssemblyName)
                .MigrationsHistoryTable("__ef_migrations_history", "public"))
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, ModuleModelCacheKeyFactory>();
    }
}
