using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Events;
using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Messaging;

namespace Finance.Modules.Transfers.Application.DomainEventHandlers;

public sealed class TransferProcessingStartedDomainEventHandler(IOutbox outbox) : DomainEventHandler<TransferProcessingStartedDomainEvent>
{
    protected override Task HandleDomainEventAsync(TransferProcessingStartedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var message = new TransferEventMessage(domainEvent.TransferId, "TransferProcessingStarted", TransferStatus.Processing, null, domainEvent.OccurredAt);
        outbox.Add(TransferRoutingKeys.ProcessingStarted, message, domainEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
