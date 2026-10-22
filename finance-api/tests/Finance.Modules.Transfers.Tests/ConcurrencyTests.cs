using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Shared.Domain;
using Finance.Testing;
using static Finance.Modules.Transfers.Tests.Support.TransfersScenario;

namespace Finance.Modules.Transfers.Tests;

public sealed class ConcurrencyTests(TransfersTestFixture fixture) : IAsyncLifetime
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
    public async Task SimultaneousTransfersSharingTheSameBalance_OnlyOneCompletes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 1000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);

        var transferResults = await Task.WhenAll(
            CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 700m, null, cancellationToken),
            CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 700m, null, cancellationToken));

        var completedTransfer = Assert.Single(transferResults, transferResult => transferResult.IsSuccessful);
        Assert.Equal(TransferStatus.Completed, completedTransfer.Success.Transfer.Status);
        var failedTransfer = Assert.Single(transferResults, transferResult => transferResult.IsFailure);
        Assert.Equal("InsufficientFunds", failedTransfer.Failure.Code);
        Assert.Equal(300m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(700m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
        Assert.Equal(2, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task SimultaneousTransfers_RespectTheAttemptLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);

        var transferResults = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 10m, null, cancellationToken)));

        Assert.Equal(5, transferResults.Count(transferResult => transferResult.IsSuccessful));
        var failedTransfers = transferResults.Where(transferResult => transferResult.IsFailure).ToList();
        Assert.Equal(3, failedTransfers.Count);
        Assert.All(failedTransfers, failedTransfer => Assert.Equal("AttemptLimitExceeded", failedTransfer.Failure.Code));
        Assert.Equal(50m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
        Assert.Equal(8, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task SimultaneousRequestsWithSameIdempotencyKey_CreateASingleTransfer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var transferResults = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, "concurrent-request-0001", cancellationToken)));

        Assert.All(transferResults, transferResult => Assert.True(transferResult.IsSuccessful));
        var transferIds = transferResults.Select(transferResult => transferResult.Success.Transfer.Id).Distinct().ToList();
        Assert.Single(transferIds);
        Assert.Equal(4, transferResults.Count(transferResult => transferResult.Success.IsIdempotentReplay));
        Assert.Equal(1, await CountTransfersAsync(environment, cancellationToken));
        Assert.Equal(400m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
    }

    [Fact]
    public async Task SimultaneousCancelAndDispatch_NeverBothSucceed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, environment.TimeProvider.GetUtcNow().AddMinutes(1), null, cancellationToken);
        var transferId = scheduleResult.Success.Transfer.Id;
        environment.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        var cancelTask = CancelTransferAsync(environment, transferId, cancellationToken);
        var dispatchTask = DispatchDueTransfersAsync(environment, cancellationToken);
        await Task.WhenAll(cancelTask, dispatchTask);

        var cancelResult = await cancelTask;
        var dispatchResult = await dispatchTask;
        var transfer = await GetTransferAsync(environment, transferId, cancellationToken);
        var finalStatus = transfer.Success.Status;
        var cancelWon = finalStatus == TransferStatus.Cancelled && dispatchResult.StartedTransferCount == 0 && cancelResult.IsSuccessful;
        var dispatchWon = finalStatus == TransferStatus.Processing
            && dispatchResult.StartedTransferCount == 1
            && IsTransferCannotBeCancelled(cancelResult);
        Assert.True(cancelWon || dispatchWon, $"Unexpected outcome: status {finalStatus}, started {dispatchResult.StartedTransferCount}, cancel successful {cancelResult.IsSuccessful}.");
    }

    private static bool IsTransferCannotBeCancelled(Either<Error, TransferResponse> cancelResult)
    {
        return cancelResult.IsFailure && cancelResult.Failure.Code == "TransferCannotBeCancelled";
    }
}
