using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Shared.Application.Persistence;

namespace Finance.Modules.Transfers.Application.UseCases.ExecuteScheduledTransfer;

public sealed class ExecuteScheduledTransferUseCase(
    IUnitOfWork unitOfWork,
    ITransferRepository transferRepository,
    TransferExecutor transferExecutor)
{
    public async Task<ScheduledTransferExecutionOutcome> ExecuteAsync(Guid transferId, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var transfer = await transferRepository.FindByIdForUpdateAsync(transferId, cancellationToken);

        if (transfer is null || transfer.Status != TransferStatus.Processing)
        {
            return ScheduledTransferExecutionOutcome.Skipped;
        }

        var execution = await transferExecutor.ExecuteAsync(transfer, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return execution.IsFailure ? ScheduledTransferExecutionOutcome.Failed : ScheduledTransferExecutionOutcome.Completed;
    }
}
