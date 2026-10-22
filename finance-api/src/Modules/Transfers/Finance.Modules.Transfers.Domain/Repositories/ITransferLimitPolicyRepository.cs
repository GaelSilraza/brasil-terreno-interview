using Finance.Modules.Transfers.Domain.Entities;

namespace Finance.Modules.Transfers.Domain.Repositories;

public interface ITransferLimitPolicyRepository
{
    Task<TransferLimitPolicy?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task UpsertAsync(TransferLimitPolicy transferLimitPolicy, CancellationToken cancellationToken);
}
