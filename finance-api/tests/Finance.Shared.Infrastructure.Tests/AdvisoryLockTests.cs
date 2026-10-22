using Finance.Shared.Application.Persistence;
using Finance.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Finance.Shared.Infrastructure.Tests;

public sealed class AdvisoryLockTests(SharedInfrastructureTestFixture fixture) : IAsyncLifetime
{
    private const string SampleLockKey = "sample-key";

    private readonly FinanceTestEnvironment environment = fixture.CreateEnvironment();

    public async ValueTask InitializeAsync()
    {
        await environment.ResetDatabaseAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await environment.DisposeAsync();
    }

    [Fact]
    public async Task LockKeyHeldByOpenTransaction_CannotBeAcquiredByAnotherTransactionUntilCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = environment.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var advisoryLock = scope.ServiceProvider.GetRequiredService<IAdvisoryLock>();
        var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await advisoryLock.AcquireTransactionLockAsync(SampleLockKey, cancellationToken);
        var acquiredWhileHeld = await TryAcquireLockInSeparateTransactionAsync(SampleLockKey, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await transaction.DisposeAsync();
        var acquiredAfterCommit = await TryAcquireLockInSeparateTransactionAsync(SampleLockKey, cancellationToken);

        Assert.False(acquiredWhileHeld);
        Assert.True(acquiredAfterCommit);
    }

    [Fact]
    public async Task AcquiringLockWithoutTransaction_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = environment.CreateScope();
        var advisoryLock = scope.ServiceProvider.GetRequiredService<IAdvisoryLock>();

        var acquireLock = () => advisoryLock.AcquireTransactionLockAsync(SampleLockKey, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(acquireLock);
    }

    private async Task<bool> TryAcquireLockInSeparateTransactionAsync(string lockKey, CancellationToken cancellationToken)
    {
        var connectionString = environment.Configuration.GetConnectionString("Finance")
            ?? throw new InvalidOperationException("The Finance connection string is not configured.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(hashtextextended(@lockKey, 0))", connection, transaction);
        command.Parameters.AddWithValue("lockKey", lockKey);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        await transaction.RollbackAsync(cancellationToken);
        return result is true;
    }
}
