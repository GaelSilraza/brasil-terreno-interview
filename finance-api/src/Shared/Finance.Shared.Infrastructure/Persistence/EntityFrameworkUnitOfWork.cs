using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Domain;

namespace Finance.Shared.Infrastructure.Persistence;

public sealed class EntityFrameworkUnitOfWork(
    FinanceDbContext dbContext,
    IDomainEventDispatcher domainEventDispatcher) : IUnitOfWork
{
    public async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var dbContextTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EntityFrameworkDatabaseTransaction(dbContextTransaction);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await DispatchDomainEventsAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var aggregatesWithDomainEvents = dbContext.ChangeTracker
                .Entries<AggregateRoot>()
                .Select(entry => entry.Entity)
                .Where(aggregate => aggregate.DomainEvents.Count > 0)
                .ToList();

            if (aggregatesWithDomainEvents.Count == 0)
            {
                return;
            }

            var domainEvents = aggregatesWithDomainEvents
                .SelectMany(aggregate => aggregate.DomainEvents)
                .ToList();

            foreach (var aggregate in aggregatesWithDomainEvents)
            {
                aggregate.ClearDomainEvents();
            }

            foreach (var domainEvent in domainEvents)
            {
                await domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            }
        }
    }
}
