using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Shared.Domain;
using Finance.Testing;
using static Finance.Modules.Transfers.Tests.Support.TransfersScenario;

namespace Finance.Modules.Transfers.Tests;

public sealed class IdempotencyTests(TransfersTestFixture fixture) : IAsyncLifetime
{
    private const string IdempotencyKey = "transfer-request-0001";

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
    public async Task RepeatedRequestWithSameIdempotencyKey_ReturnsOriginalTransferAndMovesFundsOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var firstResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, IdempotencyKey, cancellationToken);
        var secondResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, IdempotencyKey, cancellationToken);

        Assert.True(firstResult.IsSuccessful);
        Assert.True(secondResult.IsSuccessful);
        Assert.False(firstResult.Success.IsIdempotentReplay);
        Assert.True(secondResult.Success.IsIdempotentReplay);
        Assert.Equal(firstResult.Success.Transfer.Id, secondResult.Success.Transfer.Id);
        Assert.Equal(TransferStatus.Completed, secondResult.Success.Transfer.Status);
        Assert.Equal(400m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(1, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task RepeatedRejectedRequestWithSameIdempotencyKey_ReturnsTheSameFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var firstResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 1600m, IdempotencyKey, cancellationToken);
        var secondResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 1600m, IdempotencyKey, cancellationToken);

        Assert.True(firstResult.IsFailure);
        Assert.True(secondResult.IsFailure);
        Assert.Equal("InsufficientFunds", firstResult.Failure.Code);
        Assert.Equal("InsufficientFunds", secondResult.Failure.Code);
        Assert.Equal(ErrorType.BusinessRule, secondResult.Failure.Type);
        var firstTransferId = Assert.IsType<Guid>(firstResult.Failure.Metadata[TransferMetadataKeys.TransferId]);
        var secondTransferId = Assert.IsType<Guid>(secondResult.Failure.Metadata[TransferMetadataKeys.TransferId]);
        Assert.Equal(firstTransferId, secondTransferId);
        Assert.False(firstResult.Failure.Metadata.ContainsKey(ErrorMetadataKeys.IdempotentReplayed));
        Assert.Equal(true, secondResult.Failure.Metadata[ErrorMetadataKeys.IdempotentReplayed]);
        Assert.Equal(1, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task SameIdempotencyKeyWithDifferentPayload_ReturnsIdempotencyKeyReused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var firstResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, IdempotencyKey, cancellationToken);
        var secondResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 200m, IdempotencyKey, cancellationToken);

        Assert.True(firstResult.IsSuccessful);
        Assert.True(secondResult.IsFailure);
        Assert.Equal("IdempotencyKeyReused", secondResult.Failure.Code);
        Assert.Equal(400m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(1, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task RepeatedScheduleRequestWithSameIdempotencyKey_ReturnsTheOriginalScheduledTransfer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduledFor = environment.TimeProvider.GetUtcNow().AddDays(1);

        var firstResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, scheduledFor, IdempotencyKey, cancellationToken);
        var secondResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, scheduledFor, IdempotencyKey, cancellationToken);

        Assert.True(firstResult.IsSuccessful);
        Assert.True(secondResult.IsSuccessful);
        Assert.False(firstResult.Success.IsIdempotentReplay);
        Assert.True(secondResult.Success.IsIdempotentReplay);
        Assert.Equal(firstResult.Success.Transfer.Id, secondResult.Success.Transfer.Id);
        Assert.Equal(TransferStatus.Scheduled, secondResult.Success.Transfer.Status);
        Assert.Equal(1, await CountTransfersAsync(environment, cancellationToken));
    }
}
