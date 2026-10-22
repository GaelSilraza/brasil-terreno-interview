using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Application.UseCases.CancelTransfer;

public sealed class CancelTransferUseCase(
    IUnitOfWork unitOfWork,
    ITransferRepository transferRepository,
    TransferResponseFactory transferResponseFactory,
    TimeProvider timeProvider)
{
    public async Task<Either<Error, TransferResponse>> ExecuteAsync(Guid transferId, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var transfer = await transferRepository.FindByIdForUpdateAsync(transferId, cancellationToken);

        if (transfer is null)
        {
            return TransferErrors.TransferNotFound;
        }

        var cancellation = transfer.Cancel(timeProvider.GetUtcNow());

        if (cancellation.IsFailure)
        {
            return cancellation.Failure;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await transferResponseFactory.CreateAsync(TransferReadModel.FromTransfer(transfer), cancellationToken);
    }
}
