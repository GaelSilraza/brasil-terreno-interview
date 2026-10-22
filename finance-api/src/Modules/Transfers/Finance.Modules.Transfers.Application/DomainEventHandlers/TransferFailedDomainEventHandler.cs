using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Events;
using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Messaging;

namespace Finance.Modules.Transfers.Application.DomainEventHandlers;

public sealed class TransferFailedDomainEventHandler(IOutbox outbox) : DomainEventHandler<TransferFailedDomainEvent>
{
    protected override Task HandleDomainEventAsync(TransferFailedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var message = new TransferEventMessage(domainEvent.TransferId, "TransferFailed", TransferStatus.Failed, domainEvent.FailureCode, domainEvent.OccurredAt);
        outbox.Add(TransferRoutingKeys.Failed, message, domainEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
