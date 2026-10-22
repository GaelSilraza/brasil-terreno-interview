using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Domain;

namespace Finance.Shared.Infrastructure.DomainEvents;

public sealed class DomainEventDispatcher(IEnumerable<IDomainEventHandler> domainEventHandlers) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var matchingDomainEventHandlers = domainEventHandlers
            .Where(domainEventHandler => domainEventHandler.DomainEventType == domainEvent.GetType())
            .ToList();

        foreach (var domainEventHandler in matchingDomainEventHandlers)
        {
            await domainEventHandler.HandleAsync(domainEvent, cancellationToken);
        }
    }
}
