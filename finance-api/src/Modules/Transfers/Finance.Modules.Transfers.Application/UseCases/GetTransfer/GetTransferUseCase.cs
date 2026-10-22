using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Application.UseCases.GetTransfer;

public sealed class GetTransferUseCase(
    ITransferQueries transferQueries,
    TransferResponseFactory transferResponseFactory)
{
    public async Task<Either<Error, TransferResponse>> ExecuteAsync(Guid transferId, CancellationToken cancellationToken)
    {
        var transfer = await transferQueries.FindByIdAsync(transferId, cancellationToken);

        if (transfer is null)
        {
            return TransferErrors.TransferNotFound;
        }

        return await transferResponseFactory.CreateAsync(transfer, cancellationToken);
    }
}
