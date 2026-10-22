using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Application.UseCases.ExecuteScheduledTransfer;
using Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Testing;
using static Finance.Modules.Transfers.Tests.Support.TransfersScenario;

namespace Finance.Modules.Transfers.Tests;

public sealed class ScheduledTransferTests(TransfersTestFixture fixture) : IAsyncLifetime
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
    public async Task SchedulingTransfer_WithFutureDate_PersistsScheduledTransferAndScheduledEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduledFor = environment.TimeProvider.GetUtcNow().AddDays(1);

        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, scheduledFor, null, cancellationToken);

        Assert.True(scheduleResult.IsSuccessful);
        var transfer = scheduleResult.Success.Transfer;
        Assert.Equal(TransferStatus.Scheduled, transfer.Status);
        Assert.Equal(TransferType.Scheduled, transfer.Type);
        Assert.Null(transfer.AttemptedAt);
        Assert.Equal<DateTimeOffset?>(scheduledFor, transfer.ScheduledFor);
        var persistedTransfer = await GetTransferAsync(environment, transfer.Id, cancellationToken);
        Assert.Equal(TransferStatus.Scheduled, persistedTransfer.Success.Status);
        Assert.Null(persistedTransfer.Success.AttemptedAt);
        Assert.Equal(1, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.Scheduled, cancellationToken));
        Assert.Equal(500m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(0m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task SchedulingTransfer_InThePast_ReturnsMustBeInFutureFieldError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduledFor = environment.TimeProvider.GetUtcNow().AddMinutes(-1);

        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, scheduledFor, null, cancellationToken);

        Assert.True(scheduleResult.IsFailure);
        Assert.Equal("ValidationFailed", scheduleResult.Failure.Code);
        Assert.Contains(scheduleResult.Failure.FieldErrors, fieldError => fieldError.Field == "scheduledFor" && fieldError.Code == "MustBeInFuture");
        Assert.Equal(0, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task SchedulingTransfer_FromBlockedAccount_ReturnsSourceAccountInactiveAndPersistsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Daniel Rocha", 300m, 0m, "Blocked", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduledFor = environment.TimeProvider.GetUtcNow().AddDays(1);

        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, scheduledFor, null, cancellationToken);

        Assert.True(scheduleResult.IsFailure);
        Assert.Equal("SourceAccountInactive", scheduleResult.Failure.Code);
        Assert.Equal(0, await CountTransfersAsync(environment, cancellationToken));
        Assert.Equal(0, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.Scheduled, cancellationToken));
    }

    [Fact]
    public async Task SchedulingTransfer_DoesNotCheckBalanceAtSchedulingTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduledFor = environment.TimeProvider.GetUtcNow().AddDays(1);

        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 4000m, scheduledFor, null, cancellationToken);

        Assert.True(scheduleResult.IsSuccessful);
        Assert.Equal(TransferStatus.Scheduled, scheduleResult.Success.Transfer.Status);
        Assert.Equal(500m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
    }

    [Fact]
    public async Task DispatchingDueTransfers_StartsOnlyDueTransfersAndWritesProcessingStartedEvents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var now = environment.TimeProvider.GetUtcNow();
        var dueSoonTransfer = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, now.AddMinutes(10), null, cancellationToken);
        var dueLaterTransfer = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, now.AddHours(2), null, cancellationToken);

        environment.TimeProvider.Advance(TimeSpan.FromMinutes(15));
        var dispatchResult = await DispatchDueTransfersAsync(environment, cancellationToken);

        Assert.Equal(1, dispatchResult.StartedTransferCount);
        Assert.Equal(0, dispatchResult.TimedOutTransferCount);
        var dueSoonState = await GetTransferAsync(environment, dueSoonTransfer.Success.Transfer.Id, cancellationToken);
        var dueLaterState = await GetTransferAsync(environment, dueLaterTransfer.Success.Transfer.Id, cancellationToken);
        Assert.Equal(TransferStatus.Processing, dueSoonState.Success.Status);
        Assert.NotNull(dueSoonState.Success.ProcessingStartedAt);
        Assert.Equal(TransferStatus.Scheduled, dueLaterState.Success.Status);
        Assert.Equal(1, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.ProcessingStarted, cancellationToken));
    }

    [Fact]
    public async Task ExecutingScheduledTransfer_WithEnoughFundsAtExecution_Completes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, environment.TimeProvider.GetUtcNow().AddMinutes(10), null, cancellationToken);
        var transferId = scheduleResult.Success.Transfer.Id;

        environment.TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await DispatchDueTransfersAsync(environment, cancellationToken);
        var outcome = await ExecuteScheduledTransferAsync(environment, transferId, cancellationToken);

        Assert.Equal(ScheduledTransferExecutionOutcome.Completed, outcome);
        var transfer = await GetTransferAsync(environment, transferId, cancellationToken);
        Assert.Equal(TransferStatus.Completed, transfer.Success.Status);
        Assert.NotNull(transfer.Success.AttemptedAt);
        Assert.NotNull(transfer.Success.CompletedAt);
        Assert.Equal(400m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(100m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
        Assert.Equal(1, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.Completed, cancellationToken));
    }

    [Fact]
    public async Task ExecutingScheduledTransfer_WithoutFundsAtExecutionTime_FailsWithInsufficientFunds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 1000m, 0m, "Active", cancellationToken);
        var scheduledDestinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);
        var immediateDestinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, scheduledDestinationAccountId, 500m, environment.TimeProvider.GetUtcNow().AddMinutes(10), null, cancellationToken);
        var immediateResult = await CreateImmediateTransferAsync(environment, sourceAccountId, immediateDestinationAccountId, 800m, null, cancellationToken);
        var transferId = scheduleResult.Success.Transfer.Id;

        environment.TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await DispatchDueTransfersAsync(environment, cancellationToken);
        var outcome = await ExecuteScheduledTransferAsync(environment, transferId, cancellationToken);

        Assert.True(immediateResult.IsSuccessful);
        Assert.Equal(ScheduledTransferExecutionOutcome.Failed, outcome);
        var transfer = await GetTransferAsync(environment, transferId, cancellationToken);
        Assert.Equal(TransferStatus.Failed, transfer.Success.Status);
        Assert.Equal("InsufficientFunds", transfer.Success.FailureCode);
        Assert.Equal(200m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(0m, await GetBalanceAsync(environment, scheduledDestinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task ExecutingScheduledTransfer_RechecksHourlyLimitsAtExecutionTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 0m, 0m, "Active", cancellationToken);
        await SetTransferLimitsAsync(environment, new SetTransferLimitsCommand(sourceAccountId, 1000m, 10, 1000m, 10), cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 300m, environment.TimeProvider.GetUtcNow().AddMinutes(10), null, cancellationToken);
        var transferId = scheduleResult.Success.Transfer.Id;
        environment.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var immediateResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 900m, null, cancellationToken);

        environment.TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await DispatchDueTransfersAsync(environment, cancellationToken);
        var outcome = await ExecuteScheduledTransferAsync(environment, transferId, cancellationToken);

        Assert.True(immediateResult.IsSuccessful);
        Assert.Equal(ScheduledTransferExecutionOutcome.Failed, outcome);
        var transfer = await GetTransferAsync(environment, transferId, cancellationToken);
        Assert.Equal(TransferStatus.Failed, transfer.Success.Status);
        Assert.Equal("HourlyAmountLimitExceeded", transfer.Success.FailureCode);
        Assert.Equal(9100m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
    }

    [Fact]
    public async Task ExecutingSameScheduledTransferTwice_MovesFundsOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, environment.TimeProvider.GetUtcNow().AddMinutes(10), null, cancellationToken);
        var transferId = scheduleResult.Success.Transfer.Id;
        environment.TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await DispatchDueTransfersAsync(environment, cancellationToken);

        var firstOutcome = await ExecuteScheduledTransferAsync(environment, transferId, cancellationToken);
        var secondOutcome = await ExecuteScheduledTransferAsync(environment, transferId, cancellationToken);

        Assert.Equal(ScheduledTransferExecutionOutcome.Completed, firstOutcome);
        Assert.Equal(ScheduledTransferExecutionOutcome.Skipped, secondOutcome);
        Assert.Equal(400m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(100m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task CancelledTransfer_IsNeverDispatched()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, environment.TimeProvider.GetUtcNow().AddMinutes(10), null, cancellationToken);
        var transferId = scheduleResult.Success.Transfer.Id;
        var cancelResult = await CancelTransferAsync(environment, transferId, cancellationToken);

        environment.TimeProvider.Advance(TimeSpan.FromMinutes(15));
        var dispatchResult = await DispatchDueTransfersAsync(environment, cancellationToken);

        Assert.True(cancelResult.IsSuccessful);
        Assert.Equal(0, dispatchResult.StartedTransferCount);
        var transfer = await GetTransferAsync(environment, transferId, cancellationToken);
        Assert.Equal(TransferStatus.Cancelled, transfer.Success.Status);
        Assert.Equal(500m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
    }

    [Fact]
    public async Task TransferStuckInProcessing_IsFailedWithProcessingTimeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, environment.TimeProvider.GetUtcNow().AddMinutes(10), null, cancellationToken);
        var transferId = scheduleResult.Success.Transfer.Id;
        environment.TimeProvider.Advance(TimeSpan.FromMinutes(15));
        var firstDispatchResult = await DispatchDueTransfersAsync(environment, cancellationToken);

        environment.TimeProvider.Advance(TimeSpan.FromMinutes(11));
        var secondDispatchResult = await DispatchDueTransfersAsync(environment, cancellationToken);
        var outcomeAfterTimeout = await ExecuteScheduledTransferAsync(environment, transferId, cancellationToken);

        Assert.Equal(1, firstDispatchResult.StartedTransferCount);
        Assert.Equal(1, secondDispatchResult.TimedOutTransferCount);
        Assert.Equal(ScheduledTransferExecutionOutcome.Skipped, outcomeAfterTimeout);
        var transfer = await GetTransferAsync(environment, transferId, cancellationToken);
        Assert.Equal(TransferStatus.Failed, transfer.Success.Status);
        Assert.Equal("ProcessingTimeout", transfer.Success.FailureCode);
        Assert.Equal(500m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(1, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.Failed, cancellationToken));
    }
}
