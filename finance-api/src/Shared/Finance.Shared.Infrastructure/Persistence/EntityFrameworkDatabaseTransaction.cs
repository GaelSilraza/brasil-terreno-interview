using Finance.Shared.Application.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Finance.Shared.Infrastructure.Persistence;

public sealed class EntityFrameworkDatabaseTransaction(IDbContextTransaction dbContextTransaction) : IDatabaseTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken)
    {
        return dbContextTransaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return dbContextTransaction.DisposeAsync();
    }
}
