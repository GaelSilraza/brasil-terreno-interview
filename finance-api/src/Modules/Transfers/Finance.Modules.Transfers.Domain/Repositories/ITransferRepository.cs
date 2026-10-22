using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.ValueObjects;

namespace Finance.Modules.Transfers.Domain.Repositories;

public interface ITransferRepository
{
    void Add(Transfer transfer);

    Task<Transfer?> FindByIdForUpdateAsync(Guid transferId, CancellationToken cancellationToken);

    Task<Transfer?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<TransferWindowUsage> GetWindowUsageAsync(Guid sourceAccountId, DateTimeOffset windowStart, CancellationToken cancellationToken);

    Task<IReadOnlyList<Transfer>> LockDueScheduledAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Transfer>> LockStaleProcessingAsync(DateTimeOffset processingStartedBefore, int batchSize, CancellationToken cancellationToken);
}
