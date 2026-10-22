using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Modules.Accounts.Infrastructure.Persistence.Views;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Modules.Accounts.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository(FinanceDbContext dbContext) : IAccountRepository
{
    public void Add(Account account)
    {
        dbContext.Set<Account>().Add(account);
    }

    public Task<bool> ExistsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Account>().AsNoTracking().AnyAsync(account => account.Id == accountId, cancellationToken);
    }

    public async Task<IReadOnlyList<LockedAccount>> LockForTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        CancellationToken cancellationToken)
    {
        var accountIds = new[] { sourceAccountId, destinationAccountId };
        var lockedAccounts = await dbContext.Set<Account>()
            .FromSql($"SELECT * FROM accounts WHERE id = ANY({accountIds}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
        var balancesByAccountId = await dbContext.Set<AccountBalance>()
            .Where(accountBalance => accountIds.Contains(accountBalance.AccountId))
            .ToDictionaryAsync(accountBalance => accountBalance.AccountId, accountBalance => accountBalance.Balance, cancellationToken);

        return lockedAccounts
            .Select(account => new LockedAccount(account, balancesByAccountId[account.Id]))
            .ToList();
    }

    public Task<int> UpdateOverdraftLimitAsync(
        Guid accountId,
        decimal overdraftLimit,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<Account>()
            .Where(account => account.Id == accountId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(account => account.OverdraftLimit, overdraftLimit)
                    .SetProperty(account => account.UpdatedAt, updatedAt),
                cancellationToken);
    }

    public Task<int> UpdateStatusAsync(
        Guid accountId,
        AccountStatus status,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<Account>()
            .Where(account => account.Id == accountId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(account => account.Status, status)
                    .SetProperty(account => account.UpdatedAt, updatedAt),
                cancellationToken);
    }
}
