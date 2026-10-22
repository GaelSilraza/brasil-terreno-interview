using Finance.Modules.Transfers.Application.Messages;
using Finance.Shared.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace Finance.Modules.Transfers.Application.Processors;

public sealed class TransferNotificationProcessor(ILogger<TransferNotificationProcessor> logger) : IMessageProcessor<TransferEventMessage>
{
    public Task ProcessAsync(TransferEventMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Notification sent: transfer {TransferId} {EventName} with status {Status} and failure code {FailureCode}.",
            message.TransferId,
            message.EventName,
            message.Status,
            message.FailureCode);
        return Task.CompletedTask;
    }
}
