using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;

namespace Finance.Modules.Accounts.Domain.Repositories;

public interface IAccountRepository
{
    void Add(Account account);

    Task<bool> ExistsAsync(Guid accountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LockedAccount>> LockForTransferAsync(Guid sourceAccountId, Guid destinationAccountId, CancellationToken cancellationToken);

    Task<int> UpdateOverdraftLimitAsync(Guid accountId, decimal overdraftLimit, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    Task<int> UpdateStatusAsync(Guid accountId, AccountStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken);
}
