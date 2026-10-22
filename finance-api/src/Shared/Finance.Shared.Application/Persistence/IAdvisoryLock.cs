namespace Finance.Shared.Application.Persistence;

public interface IAdvisoryLock
{
    Task AcquireTransactionLockAsync(string lockKey, CancellationToken cancellationToken);
}
