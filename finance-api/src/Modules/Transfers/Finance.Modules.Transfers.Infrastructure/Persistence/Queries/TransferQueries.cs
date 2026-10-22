using System.Linq.Expressions;
using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Domain.Entities;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Modules.Transfers.Infrastructure.Persistence.Queries;

public sealed class TransferQueries(FinanceDbContext dbContext) : ITransferQueries
{
    private static readonly Expression<Func<Transfer, TransferReadModel>> ToReadModel = transfer => new TransferReadModel(
        transfer.Id,
        transfer.SourceAccountId,
        transfer.DestinationAccountId,
        transfer.Amount,
        transfer.Type,
        transfer.Status,
        transfer.ScheduledFor,
        transfer.AttemptedAt,
        transfer.ProcessingStartedAt,
        transfer.CompletedAt,
        transfer.FailedAt,
        transfer.CancelledAt,
        transfer.FailureCode,
        transfer.CreatedAt,
        transfer.UpdatedAt);

    public Task<TransferReadModel?> FindByIdAsync(Guid transferId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Transfer>()
            .AsNoTracking()
            .Where(transfer => transfer.Id == transferId)
            .Select(ToReadModel)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CursorPage<TransferReadModel>> ListByAccountAsync(
        Guid accountId,
        CursorPageRequest<TransferCursorKey> pageRequest,
        CancellationToken cancellationToken)
    {
        var sent = dbContext.Set<Transfer>().AsNoTracking().Where(transfer => transfer.SourceAccountId == accountId);
        var received = dbContext.Set<Transfer>().AsNoTracking().Where(transfer => transfer.DestinationAccountId == accountId);

        if (pageRequest.After is { } after)
        {
            sent = pageRequest.Direction == CursorDirection.Next ? sent.Where(transfer => transfer.Id < after.Id) : sent.Where(transfer => transfer.Id > after.Id);
            received = pageRequest.Direction == CursorDirection.Next ? received.Where(transfer => transfer.Id < after.Id) : received.Where(transfer => transfer.Id > after.Id);
        }

        var fetchCount = pageRequest.Limit + 1;
        var combined = OrderInReadDirection(sent, pageRequest.Direction).Take(fetchCount)
            .Concat(OrderInReadDirection(received, pageRequest.Direction).Take(fetchCount));

        var fetched = await OrderInReadDirection(combined, pageRequest.Direction)
            .Take(fetchCount)
            .Select(ToReadModel)
            .ToListAsync(cancellationToken);
        return CursorPageBuilder.Build(fetched, pageRequest, transfer => new TransferCursorKey(transfer.Id));
    }

    private static IQueryable<Transfer> OrderInReadDirection(IQueryable<Transfer> transfers, CursorDirection direction)
    {
        return direction == CursorDirection.Next
            ? transfers.OrderByDescending(transfer => transfer.Id)
            : transfers.OrderBy(transfer => transfer.Id);
    }
}
