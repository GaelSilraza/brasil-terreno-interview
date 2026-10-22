using Finance.Shared.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Shared.Infrastructure.Persistence;

public sealed class PostgresAdvisoryLock(FinanceDbContext dbContext) : IAdvisoryLock
{
    public async Task AcquireTransactionLockAsync(string lockKey, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A transaction-scoped advisory lock requires an active transaction.");
        }

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }
}
