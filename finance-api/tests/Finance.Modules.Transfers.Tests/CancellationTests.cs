using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Testing;
using static Finance.Modules.Transfers.Tests.Support.TransfersScenario;

namespace Finance.Modules.Transfers.Tests;

public sealed class CancellationTests(TransfersTestFixture fixture) : IAsyncLifetime
{
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
    public async Task CancellingScheduledTransfer_MarksItCancelledAndWritesCancelledEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, environment.TimeProvider.GetUtcNow().AddDays(1), null, cancellationToken);

        var cancelResult = await CancelTransferAsync(environment, scheduleResult.Success.Transfer.Id, cancellationToken);

        Assert.True(cancelResult.IsSuccessful);
        Assert.Equal(TransferStatus.Cancelled, cancelResult.Success.Status);
        Assert.NotNull(cancelResult.Success.CancelledAt);
        var persistedTransfer = await GetTransferAsync(environment, scheduleResult.Success.Transfer.Id, cancellationToken);
        Assert.Equal(TransferStatus.Cancelled, persistedTransfer.Success.Status);
        Assert.NotNull(persistedTransfer.Success.CancelledAt);
        Assert.Equal(1, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.Cancelled, cancellationToken));
    }

    [Fact]
    public async Task CancellingCompletedTransfer_ReturnsTransferCannotBeCancelled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, null, cancellationToken);

        var cancelResult = await CancelTransferAsync(environment, transferResult.Success.Transfer.Id, cancellationToken);

        Assert.True(cancelResult.IsFailure);
        Assert.Equal("TransferCannotBeCancelled", cancelResult.Failure.Code);
        var persistedTransfer = await GetTransferAsync(environment, transferResult.Success.Transfer.Id, cancellationToken);
        Assert.Equal(TransferStatus.Completed, persistedTransfer.Success.Status);
    }

    [Fact]
    public async Task CancellingTransferInProcessing_ReturnsTransferCannotBeCancelled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, environment.TimeProvider.GetUtcNow().AddMinutes(10), null, cancellationToken);
        environment.TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await DispatchDueTransfersAsync(environment, cancellationToken);

        var cancelResult = await CancelTransferAsync(environment, scheduleResult.Success.Transfer.Id, cancellationToken);

        Assert.True(cancelResult.IsFailure);
        Assert.Equal("TransferCannotBeCancelled", cancelResult.Failure.Code);
        var persistedTransfer = await GetTransferAsync(environment, scheduleResult.Success.Transfer.Id, cancellationToken);
        Assert.Equal(TransferStatus.Processing, persistedTransfer.Success.Status);
    }

    [Fact]
    public async Task CancellingUnknownTransfer_ReturnsTransferNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var unknownTransferId = Guid.CreateVersion7(environment.TimeProvider.GetUtcNow());

        var cancelResult = await CancelTransferAsync(environment, unknownTransferId, cancellationToken);

        Assert.True(cancelResult.IsFailure);
        Assert.Equal("TransferNotFound", cancelResult.Failure.Code);
    }
}
