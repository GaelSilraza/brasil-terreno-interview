namespace Finance.Shared.Application.Persistence;

public interface IDatabaseTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
