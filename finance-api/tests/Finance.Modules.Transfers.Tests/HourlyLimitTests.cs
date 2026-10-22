using Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Testing;
using static Finance.Modules.Transfers.Tests.Support.TransfersScenario;

namespace Finance.Modules.Transfers.Tests;

public sealed class HourlyLimitTests(TransfersTestFixture fixture) : IAsyncLifetime
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
    public async Task TransfersAboveHourlyAmountLimit_FailWithHourlyAmountLimitExceeded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);

        var firstTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 3000m, null, cancellationToken);
        var secondTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 2500m, null, cancellationToken);
        var thirdTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 2000m, null, cancellationToken);

        Assert.True(firstTransfer.IsSuccessful);
        Assert.True(secondTransfer.IsFailure);
        Assert.Equal("HourlyAmountLimitExceeded", secondTransfer.Failure.Code);
        Assert.True(thirdTransfer.IsSuccessful);
        Assert.Equal(5000m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
        Assert.Equal(5000m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
    }

    [Fact]
    public async Task AttemptsBeyondHourlyLimit_FailWithAttemptLimitExceeded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);
        await CompleteTransfersAsync(sourceAccountId, destinationAccountId, 10m, 5, cancellationToken);

        var sixthTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 10m, null, cancellationToken);

        Assert.True(sixthTransfer.IsFailure);
        Assert.Equal("AttemptLimitExceeded", sixthTransfer.Failure.Code);
        Assert.Equal(50m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
        Assert.Equal(6, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task RejectedTransfers_CountAsAttempts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var rejectedFailureCodes = new List<string>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var rejectedTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 10m, null, cancellationToken);
            rejectedFailureCodes.Add(rejectedTransfer.Failure.Code);
        }

        var sixthTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 10m, null, cancellationToken);

        Assert.All(rejectedFailureCodes, failureCode => Assert.Equal("InsufficientFunds", failureCode));
        Assert.True(sixthTransfer.IsFailure);
        Assert.Equal("AttemptLimitExceeded", sixthTransfer.Failure.Code);
    }

    [Fact]
    public async Task FailedTransfers_DoNotConsumeTheHourlyAmount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 4000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var failedTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 4500m, null, cancellationToken);
        var completedTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 4000m, null, cancellationToken);

        Assert.True(failedTransfer.IsFailure);
        Assert.Equal("InsufficientFunds", failedTransfer.Failure.Code);
        Assert.True(completedTransfer.IsSuccessful);
        Assert.Equal(TransferStatus.Completed, completedTransfer.Success.Transfer.Status);
        Assert.Equal(0m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
    }

    [Fact]
    public async Task HourlyWindowIsRolling_LimitsAreAvailableAgainAfterSixtyMinutes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);
        await CompleteTransfersAsync(sourceAccountId, destinationAccountId, 10m, 5, cancellationToken);

        environment.TimeProvider.Advance(TimeSpan.FromMinutes(61));
        var nextTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 10m, null, cancellationToken);

        Assert.True(nextTransfer.IsSuccessful);
        Assert.Equal(TransferStatus.Completed, nextTransfer.Success.Transfer.Status);
    }

    [Fact]
    public async Task NighttimeTransfer_UsesNighttimeAmountLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        environment.TimeProvider.SetUtcNow(TestClock.NighttimeInSaoPaulo);
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);

        var aboveLimitTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 1200m, null, cancellationToken);
        var withinLimitTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 1000m, null, cancellationToken);

        Assert.True(aboveLimitTransfer.IsFailure);
        Assert.Equal("HourlyAmountLimitExceeded", aboveLimitTransfer.Failure.Code);
        Assert.True(withinLimitTransfer.IsSuccessful);
        Assert.Equal(1000m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task NighttimeTransfer_AllowsOnlyThreeAttemptsPerHour()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        environment.TimeProvider.SetUtcNow(TestClock.NighttimeInSaoPaulo);
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);
        await CompleteTransfersAsync(sourceAccountId, destinationAccountId, 10m, 3, cancellationToken);

        var fourthTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 10m, null, cancellationToken);

        Assert.True(fourthTransfer.IsFailure);
        Assert.Equal("AttemptLimitExceeded", fourthTransfer.Failure.Code);
        Assert.Equal(30m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task CustomLimits_OverrideTheDefaults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);
        var setLimits = await SetTransferLimitsAsync(environment, new SetTransferLimitsCommand(sourceAccountId, 100m, 2, 100m, 2), cancellationToken);

        var aboveAmountTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 150m, null, cancellationToken);
        var withinLimitsTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 10m, null, cancellationToken);
        var beyondAttemptsTransfer = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 10m, null, cancellationToken);

        Assert.True(setLimits.IsSuccessful);
        Assert.True(aboveAmountTransfer.IsFailure);
        Assert.Equal("HourlyAmountLimitExceeded", aboveAmountTransfer.Failure.Code);
        Assert.True(withinLimitsTransfer.IsSuccessful);
        Assert.True(beyondAttemptsTransfer.IsFailure);
        Assert.Equal("AttemptLimitExceeded", beyondAttemptsTransfer.Failure.Code);
    }

    private async Task CompleteTransfersAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        int transferCount,
        CancellationToken cancellationToken)
    {
        for (var transferNumber = 0; transferNumber < transferCount; transferNumber++)
        {
            var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, amount, null, cancellationToken);
            Assert.True(transferResult.IsSuccessful);
        }
    }
}
