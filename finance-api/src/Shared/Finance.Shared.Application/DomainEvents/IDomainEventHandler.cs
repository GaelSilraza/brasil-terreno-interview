using Finance.Shared.Domain;

namespace Finance.Shared.Application.DomainEvents;

public interface IDomainEventHandler
{
    Type DomainEventType { get; }

    Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}
