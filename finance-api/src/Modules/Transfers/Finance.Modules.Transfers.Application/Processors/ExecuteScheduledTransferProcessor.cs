using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Application.UseCases.ExecuteScheduledTransfer;
using Finance.Shared.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace Finance.Modules.Transfers.Application.Processors;

public sealed class ExecuteScheduledTransferProcessor(
    ExecuteScheduledTransferUseCase executeScheduledTransferUseCase,
    ILogger<ExecuteScheduledTransferProcessor> logger) : IMessageProcessor<TransferEventMessage>
{
    public async Task ProcessAsync(TransferEventMessage message, CancellationToken cancellationToken)
    {
        var outcome = await executeScheduledTransferUseCase.ExecuteAsync(message.TransferId, cancellationToken);
        logger.LogInformation("Scheduled transfer {TransferId} execution finished with outcome {Outcome}.", message.TransferId, outcome);
    }
}
