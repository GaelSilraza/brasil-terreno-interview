using Finance.Shared.Domain;

namespace Finance.Shared.Application.DomainEvents;

public abstract class DomainEventHandler<TDomainEvent> : IDomainEventHandler
    where TDomainEvent : IDomainEvent
{
    public Type DomainEventType => typeof(TDomainEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        return HandleDomainEventAsync((TDomainEvent)domainEvent, cancellationToken);
    }

    protected abstract Task HandleDomainEventAsync(TDomainEvent domainEvent, CancellationToken cancellationToken);
}
