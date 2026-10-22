using Finance.Shared.Application.Pagination;

namespace Finance.Modules.Transfers.Application.Queries;

public interface ITransferQueries
{
    Task<TransferReadModel?> FindByIdAsync(Guid transferId, CancellationToken cancellationToken);

    Task<CursorPage<TransferReadModel>> ListByAccountAsync(
        Guid accountId,
        CursorPageRequest<TransferCursorKey> pageRequest,
        CancellationToken cancellationToken);
}
