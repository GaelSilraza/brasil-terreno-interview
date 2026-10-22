using Finance.Modules.Transfers.Application.Options;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Shared.Application.Persistence;
using Microsoft.Extensions.Options;

namespace Finance.Modules.Transfers.Application.UseCases.DispatchDueScheduledTransfers;

public sealed class DispatchDueScheduledTransfersUseCase(
    IUnitOfWork unitOfWork,
    ITransferRepository transferRepository,
    IOptions<ScheduledTransfersOptions> scheduledTransfersOptions,
    TimeProvider timeProvider)
{
    public async Task<DispatchDueScheduledTransfersResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var options = scheduledTransfersOptions.Value;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var dueTransfers = await transferRepository.LockDueScheduledAsync(now, options.BatchSize, cancellationToken);

        foreach (var dueTransfer in dueTransfers)
        {
            dueTransfer.StartProcessing(now);
        }

        var processingStartedBefore = now - TimeSpan.FromMinutes(options.StaleProcessingTimeoutMinutes);
        var staleTransfers = await transferRepository.LockStaleProcessingAsync(processingStartedBefore, options.BatchSize, cancellationToken);

        foreach (var staleTransfer in staleTransfers)
        {
            staleTransfer.Fail(TransferErrors.ProcessingTimeout, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DispatchDueScheduledTransfersResult(dueTransfers.Count, staleTransfers.Count);
    }
}
