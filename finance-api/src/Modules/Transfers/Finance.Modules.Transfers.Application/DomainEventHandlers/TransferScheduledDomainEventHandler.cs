using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Events;
using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Messaging;

namespace Finance.Modules.Transfers.Application.DomainEventHandlers;

public sealed class TransferScheduledDomainEventHandler(IOutbox outbox) : DomainEventHandler<TransferScheduledDomainEvent>
{
    protected override Task HandleDomainEventAsync(TransferScheduledDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var message = new TransferEventMessage(domainEvent.TransferId, "TransferScheduled", TransferStatus.Scheduled, null, domainEvent.OccurredAt);
        outbox.Add(TransferRoutingKeys.Scheduled, message, domainEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
