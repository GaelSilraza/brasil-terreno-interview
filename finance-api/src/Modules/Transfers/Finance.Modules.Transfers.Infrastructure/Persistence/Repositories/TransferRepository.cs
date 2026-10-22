using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Modules.Transfers.Domain.ValueObjects;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Modules.Transfers.Infrastructure.Persistence.Repositories;

public sealed class TransferRepository(FinanceDbContext dbContext) : ITransferRepository
{
    public void Add(Transfer transfer)
    {
        dbContext.Set<Transfer>().Add(transfer);
    }

    public Task<Transfer?> FindByIdForUpdateAsync(Guid transferId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Transfer>()
            .FromSql($"SELECT * FROM transfers WHERE id = {transferId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Transfer?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        return dbContext.Set<Transfer>()
            .AsNoTracking()
            .FirstOrDefaultAsync(transfer => transfer.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<TransferWindowUsage> GetWindowUsageAsync(
        Guid sourceAccountId,
        DateTimeOffset windowStart,
        CancellationToken cancellationToken)
    {
        var windowUsage = await dbContext.Set<Transfer>()
            .AsNoTracking()
            .Where(transfer => transfer.SourceAccountId == sourceAccountId && transfer.AttemptedAt >= windowStart)
            .GroupBy(transfer => transfer.SourceAccountId)
            .Select(attemptsInWindow => new TransferWindowUsage(
                attemptsInWindow.Count(),
                attemptsInWindow.Sum(transfer => transfer.Status == TransferStatus.Completed ? transfer.Amount : 0m)))
            .SingleOrDefaultAsync(cancellationToken);

        return windowUsage ?? new TransferWindowUsage(0, 0m);
    }

    public async Task<IReadOnlyList<Transfer>> LockDueScheduledAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<Transfer>()
            .FromSql($"SELECT * FROM transfers WHERE status = 'Scheduled' AND scheduled_for <= {now} ORDER BY scheduled_for LIMIT {batchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transfer>> LockStaleProcessingAsync(
        DateTimeOffset processingStartedBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<Transfer>()
            .FromSql($"SELECT * FROM transfers WHERE status = 'Processing' AND processing_started_at < {processingStartedBefore} ORDER BY processing_started_at LIMIT {batchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
    }
}
