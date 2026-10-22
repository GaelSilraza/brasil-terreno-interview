using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Events;
using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Messaging;

namespace Finance.Modules.Transfers.Application.DomainEventHandlers;

public sealed class TransferCancelledDomainEventHandler(IOutbox outbox) : DomainEventHandler<TransferCancelledDomainEvent>
{
    protected override Task HandleDomainEventAsync(TransferCancelledDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var message = new TransferEventMessage(domainEvent.TransferId, "TransferCancelled", TransferStatus.Cancelled, null, domainEvent.OccurredAt);
        outbox.Add(TransferRoutingKeys.Cancelled, message, domainEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
