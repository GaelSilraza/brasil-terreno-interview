using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Events;
using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Messaging;

namespace Finance.Modules.Transfers.Application.DomainEventHandlers;

public sealed class TransferCompletedDomainEventHandler(IOutbox outbox) : DomainEventHandler<TransferCompletedDomainEvent>
{
    protected override Task HandleDomainEventAsync(TransferCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var message = new TransferEventMessage(domainEvent.TransferId, "TransferCompleted", TransferStatus.Completed, null, domainEvent.OccurredAt);
        outbox.Add(TransferRoutingKeys.Completed, message, domainEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
