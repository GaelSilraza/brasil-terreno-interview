using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Modules.Transfers.Infrastructure.Persistence.Repositories;

public sealed class TransferLimitPolicyRepository(FinanceDbContext dbContext) : ITransferLimitPolicyRepository
{
    public Task<TransferLimitPolicy?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<TransferLimitPolicy>()
            .AsNoTracking()
            .FirstOrDefaultAsync(policy => policy.AccountId == accountId, cancellationToken);
    }

    public async Task UpsertAsync(TransferLimitPolicy transferLimitPolicy, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO transfer_limit_policies
                (account_id, daytime_max_amount_per_hour, daytime_max_attempts_per_hour, nighttime_max_amount_per_hour, nighttime_max_attempts_per_hour, updated_at)
            VALUES
                ({transferLimitPolicy.AccountId}, {transferLimitPolicy.DaytimeMaxAmountPerHour}, {transferLimitPolicy.DaytimeMaxAttemptsPerHour}, {transferLimitPolicy.NighttimeMaxAmountPerHour}, {transferLimitPolicy.NighttimeMaxAttemptsPerHour}, {transferLimitPolicy.UpdatedAt})
            ON CONFLICT (account_id) DO UPDATE SET
                daytime_max_amount_per_hour = EXCLUDED.daytime_max_amount_per_hour,
                daytime_max_attempts_per_hour = EXCLUDED.daytime_max_attempts_per_hour,
                nighttime_max_amount_per_hour = EXCLUDED.nighttime_max_amount_per_hour,
                nighttime_max_attempts_per_hour = EXCLUDED.nighttime_max_attempts_per_hour,
                updated_at = EXCLUDED.updated_at
            """,
            cancellationToken);
    }
}
