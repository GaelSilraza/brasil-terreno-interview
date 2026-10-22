using System.Net;
using Finance.EndToEnd.Tests.Support;
using Finance.Testing;

namespace Finance.EndToEnd.Tests;

public sealed class ScheduledTransferEndToEndTests(EndToEndTestFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(30);

    private FinanceApiClient ApiClient => new(fixture.HttpClient);

    public async ValueTask InitializeAsync()
    {
        await fixture.StartHostsAsync();
        await using var environment = fixture.CreateEnvironment();
        await environment.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        fixture.Clock.SetUtcNow(TestClock.DaytimeInSaoPaulo);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ScheduledTransfer_IsExecutedByTheWorkerThroughRabbitMq()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        using var response = await ApiClient.PostScheduledTransferAsync(
            sourceAccount.Id,
            destinationAccount.Id,
            150m,
            fixture.Clock.GetUtcNow().AddSeconds(2),
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var scheduledTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(response, cancellationToken);
        Assert.Equal("Scheduled", scheduledTransfer.Status);
        Assert.Equal("Scheduled", scheduledTransfer.Type);
        Assert.Null(scheduledTransfer.AttemptedAt);
        var executedTransfer = await WaitForFinalStatusAsync(scheduledTransfer.Id, cancellationToken);
        Assert.Equal("Completed", executedTransfer.Status);
        Assert.NotNull(executedTransfer.ProcessingStartedAt);
        Assert.NotNull(executedTransfer.CompletedAt);
        Assert.Equal(350m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(150m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task ScheduledTransferWithoutFundsAtExecution_EndsFailedWithInsufficientFunds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 1000m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        using var scheduleResponse = await ApiClient.PostScheduledTransferAsync(
            sourceAccount.Id,
            destinationAccount.Id,
            500m,
            fixture.Clock.GetUtcNow().AddSeconds(3),
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, scheduleResponse.StatusCode);
        var scheduledTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(scheduleResponse, cancellationToken);

        using var immediateResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 800m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, immediateResponse.StatusCode);
        var executedTransfer = await WaitForFinalStatusAsync(scheduledTransfer.Id, cancellationToken);
        Assert.Equal("Failed", executedTransfer.Status);
        Assert.Equal("InsufficientFunds", executedTransfer.FailureCode);
        Assert.NotNull(executedTransfer.AttemptedAt);
        Assert.Equal(200m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(800m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task CancelledScheduledTransfer_IsNeverExecuted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        using var scheduleResponse = await ApiClient.PostScheduledTransferAsync(
            sourceAccount.Id,
            destinationAccount.Id,
            100m,
            fixture.Clock.GetUtcNow().AddSeconds(3),
            null,
            cancellationToken);
        var scheduledTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(scheduleResponse, cancellationToken);

        using var cancelResponse = await ApiClient.CancelTransferAsync(scheduledTransfer.Id, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelledTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(cancelResponse, cancellationToken);
        Assert.Equal("Cancelled", cancelledTransfer.Status);
        Assert.NotNull(cancelledTransfer.CancelledAt);
        await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken);
        var persistedTransfer = await ApiClient.ReadTransferAsync(scheduledTransfer.Id, cancellationToken);
        Assert.Equal("Cancelled", persistedTransfer.Status);
        Assert.Null(persistedTransfer.AttemptedAt);
        Assert.Equal(500m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(0m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task CancellingTwice_ReturnsCancelledThen409()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 1000m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        using var scheduleResponse = await ApiClient.PostScheduledTransferAsync(
            sourceAccount.Id,
            destinationAccount.Id,
            100m,
            fixture.Clock.GetUtcNow().AddHours(1),
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, scheduleResponse.StatusCode);
        var scheduledTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(scheduleResponse, cancellationToken);

        using var firstCancelResponse = await ApiClient.CancelTransferAsync(scheduledTransfer.Id, cancellationToken);
        using var secondCancelResponse = await ApiClient.CancelTransferAsync(scheduledTransfer.Id, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstCancelResponse.StatusCode);
        var cancelledTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(firstCancelResponse, cancellationToken);
        Assert.Equal("Cancelled", cancelledTransfer.Status);
        Assert.Equal(HttpStatusCode.Conflict, secondCancelResponse.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(secondCancelResponse, cancellationToken);
        Assert.Equal("TransferCannotBeCancelled", problem.Code);
        var persistedTransfer = await ApiClient.ReadTransferAsync(scheduledTransfer.Id, cancellationToken);
        Assert.Equal("Cancelled", persistedTransfer.Status);
        Assert.Equal(1000m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task SchedulingInThePast_Returns400MustBeInFutureAndPersistsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        using var response = await ApiClient.PostScheduledTransferAsync(
            sourceAccount.Id,
            destinationAccount.Id,
            100m,
            fixture.Clock.GetUtcNow().AddMinutes(-5),
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.FieldErrors);
        Assert.Contains(problem.FieldErrors, fieldError => fieldError.Field == "scheduledFor" && fieldError.Code == "MustBeInFuture");
        var transfers = await ApiClient.ListTransfersAsync(sourceAccount.Id, null, null, null, cancellationToken);
        Assert.Empty(transfers.Items);
    }

    private Task<TransferPayload> WaitForFinalStatusAsync(Guid transferId, CancellationToken cancellationToken)
    {
        return Eventually.WaitUntilAsync(
            probeCancellationToken => ApiClient.ReadTransferAsync(transferId, probeCancellationToken),
            transfer => transfer.Status is "Completed" or "Failed",
            ExecutionTimeout,
            cancellationToken);
    }
}
