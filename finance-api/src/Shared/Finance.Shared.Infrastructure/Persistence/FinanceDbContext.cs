using Finance.Shared.Domain;
using Finance.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Finance.Shared.Infrastructure.Persistence;

public sealed class FinanceDbContext(
    DbContextOptions<FinanceDbContext> options,
    IEnumerable<IModuleDatabaseConfiguration> moduleDatabaseConfigurations) : DbContext(options)
{
    private readonly IReadOnlyList<IModuleDatabaseConfiguration> orderedModuleDatabaseConfigurations =
        moduleDatabaseConfigurations.OrderBy(moduleDatabaseConfiguration => moduleDatabaseConfiguration.ApplyOrder).ToList();

    public string ModelCacheKey => string.Join(
        '|',
        orderedModuleDatabaseConfigurations.Select(moduleDatabaseConfiguration => moduleDatabaseConfiguration.GetType().FullName));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        foreach (var moduleDatabaseConfiguration in orderedModuleDatabaseConfigurations)
        {
            moduleDatabaseConfiguration.Configure(modelBuilder);
        }

        var aggregateEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => typeof(AggregateRoot).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToList();

        foreach (var aggregateEntityType in aggregateEntityTypes)
        {
            modelBuilder.Entity(aggregateEntityType).Ignore(nameof(AggregateRoot.DomainEvents));
        }
    }
}
