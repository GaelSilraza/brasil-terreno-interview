using System.Net;
using Finance.EndToEnd.Tests.Support;
using Finance.Modules.Accounts.Domain.Entities;
using Finance.Testing;

namespace Finance.EndToEnd.Tests;

public sealed class TransfersEndpointTests(EndToEndTestFixture fixture) : IAsyncLifetime
{
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
    public async Task ImmediateTransfer_Returns201CompletedAndMovesFunds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 1000m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        using var response = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 100m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transfer = await FinanceApiClient.ReadAsync<TransferPayload>(response, cancellationToken);
        Assert.Equal($"/api/transfers/{transfer.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Completed", transfer.Status);
        Assert.Equal("Immediate", transfer.Type);
        Assert.Equal(100m, transfer.Amount);
        Assert.Equal("Ana Souza", transfer.SourceHolderName);
        Assert.Equal("Bruno Lima", transfer.DestinationHolderName);
        Assert.NotNull(transfer.AttemptedAt);
        Assert.NotNull(transfer.CompletedAt);
        Assert.Null(transfer.FailureCode);
        Assert.Equal(400m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(100m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task ImmediateTransferUsingOverdraft_Returns201AndLeavesBalanceNegative()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 1000m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        using var response = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 1000m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transfer = await FinanceApiClient.ReadAsync<TransferPayload>(response, cancellationToken);
        Assert.Equal("Completed", transfer.Status);
        using var sourceResponse = await ApiClient.GetAccountAsync(sourceAccount.Id, cancellationToken);
        var source = await FinanceApiClient.ReadAsync<AccountPayload>(sourceResponse, cancellationToken);
        Assert.Equal(-500m, source.Balance);
        Assert.Equal(500m, source.UsedOverdraft);
        Assert.Equal(500m, source.AvailableAmount);
        Assert.Equal(1000m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task ImmediateTransferWithoutFunds_Returns422WithCodeAndTransferId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 1000m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        using var response = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 1500.01m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("InsufficientFunds", problem.Code);
        Assert.NotNull(problem.TransferId);
        var transfer = await ApiClient.ReadTransferAsync(problem.TransferId.Value, cancellationToken);
        Assert.Equal("Failed", transfer.Status);
        Assert.Equal("InsufficientFunds", transfer.FailureCode);
        Assert.Equal(500m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(0m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task TransferFromBlockedAccount_Returns422SourceAccountInactive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Daniel Rocha", 1000m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        using var blockResponse = await ApiClient.UpdateAccountStatusAsync(sourceAccount.Id, "Blocked", cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, blockResponse.StatusCode);

        using var response = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 100m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("SourceAccountInactive", problem.Code);
        Assert.NotNull(problem.TransferId);
        var transfer = await ApiClient.ReadTransferAsync(problem.TransferId.Value, cancellationToken);
        Assert.Equal("Failed", transfer.Status);
        Assert.Equal("SourceAccountInactive", transfer.FailureCode);
        using var sourceResponse = await ApiClient.GetAccountAsync(sourceAccount.Id, cancellationToken);
        var source = await FinanceApiClient.ReadAsync<AccountPayload>(sourceResponse, cancellationToken);
        Assert.Equal("Blocked", source.Status);
        Assert.Equal(1000m, source.Balance);
        Assert.Equal(0m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task TransferThatWouldExceedTheMaximumBalance_Returns422AndPersistsFailedTransfer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 100m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", Account.MaximumBalance, 0m, cancellationToken);

        using var response = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 0.01m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("DestinationBalanceLimitExceeded", problem.Code);
        Assert.NotNull(problem.TransferId);
        var transfer = await ApiClient.ReadTransferAsync(problem.TransferId.Value, cancellationToken);
        Assert.Equal("Failed", transfer.Status);
        Assert.Equal("DestinationBalanceLimitExceeded", transfer.FailureCode);
        Assert.Equal(100m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(Account.MaximumBalance, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task ImmediateTransferWithUnknownAccount_Returns404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);

        using var response = await ApiClient.PostTransferAsync(sourceAccount.Id, Guid.CreateVersion7(), 100m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("DestinationAccountNotFound", problem.Code);
        Assert.Null(problem.TransferId);
    }

    [Fact]
    public async Task ImmediateTransferToSameAccount_Returns400()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var account = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);

        using var response = await ApiClient.PostTransferAsync(account.Id, account.Id, 100m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.Errors);
        Assert.True(problem.Errors.ContainsKey("destinationAccountId"));
        Assert.NotNull(problem.FieldErrors);
        Assert.Contains(problem.FieldErrors, fieldError => fieldError.Field == "destinationAccountId" && fieldError.Code == "MustBeDifferent");
    }

    [Fact]
    public async Task RepeatedRequestWithIdempotencyKey_Returns201WithReplayHeaderAndMovesFundsOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        var idempotencyKey = Guid.CreateVersion7().ToString();
        using var firstResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 100m, idempotencyKey, cancellationToken);
        var firstTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(firstResponse, cancellationToken);

        using var secondResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 100m, idempotencyKey, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.False(firstResponse.Headers.Contains(FinanceApiClient.IdempotentReplayedHeader));
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal("true", Assert.Single(secondResponse.Headers.GetValues(FinanceApiClient.IdempotentReplayedHeader)));
        var secondTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(secondResponse, cancellationToken);
        Assert.Equal(firstTransfer.Id, secondTransfer.Id);
        Assert.Equal("Completed", secondTransfer.Status);
        Assert.Equal(400m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(100m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task RepeatedRejectedRequestWithIdempotencyKey_Returns422WithReplayHeader()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 100m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        var idempotencyKey = Guid.CreateVersion7().ToString();
        using var firstResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 500m, idempotencyKey, cancellationToken);
        var firstProblem = await FinanceApiClient.ReadAsync<ProblemPayload>(firstResponse, cancellationToken);

        using var secondResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 500m, idempotencyKey, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, firstResponse.StatusCode);
        Assert.False(firstResponse.Headers.Contains(FinanceApiClient.IdempotentReplayedHeader));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondResponse.StatusCode);
        Assert.Equal("true", Assert.Single(secondResponse.Headers.GetValues(FinanceApiClient.IdempotentReplayedHeader)));
        var secondProblem = await FinanceApiClient.ReadAsync<ProblemPayload>(secondResponse, cancellationToken);
        Assert.Equal("InsufficientFunds", firstProblem.Code);
        Assert.Equal("InsufficientFunds", secondProblem.Code);
        Assert.NotNull(firstProblem.TransferId);
        Assert.Equal(firstProblem.TransferId, secondProblem.TransferId);
    }

    [Fact]
    public async Task ReusedIdempotencyKeyWithDifferentPayload_Returns422IdempotencyKeyReused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        var idempotencyKey = Guid.CreateVersion7().ToString();
        using var firstResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 100m, idempotencyKey, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 200m, idempotencyKey, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondResponse.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(secondResponse, cancellationToken);
        Assert.Equal("IdempotencyKeyReused", problem.Code);
        Assert.Null(problem.TransferId);
        Assert.Equal(400m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task ListingTransfersWithoutAccountId_Returns400()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await ApiClient.GetTransfersAsync(null, null, null, null, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.FieldErrors);
        Assert.Contains(problem.FieldErrors, fieldError => fieldError.Field == "accountId" && fieldError.Code == "Required");
    }

    [Fact]
    public async Task ListingAccountTransfers_ReturnsSentAndReceived()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var secondAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 500m, 0m, cancellationToken);
        var otherAccount = await ApiClient.OpenAccountAsync("Carla Mendes", 500m, 0m, cancellationToken);
        using var sentResponse = await ApiClient.PostTransferAsync(firstAccount.Id, secondAccount.Id, 100m, null, cancellationToken);
        var sentTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(sentResponse, cancellationToken);
        using var receivedResponse = await ApiClient.PostTransferAsync(secondAccount.Id, firstAccount.Id, 50m, null, cancellationToken);
        var receivedTransfer = await FinanceApiClient.ReadAsync<TransferPayload>(receivedResponse, cancellationToken);
        using var unrelatedResponse = await ApiClient.PostTransferAsync(secondAccount.Id, otherAccount.Id, 10m, null, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, unrelatedResponse.StatusCode);

        using var response = await ApiClient.GetTransfersAsync(firstAccount.Id, null, null, null, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transfers = await FinanceApiClient.ReadAsync<CursorPagePayload<TransferPayload>>(response, cancellationToken);
        Assert.Equal([receivedTransfer.Id, sentTransfer.Id], transfers.Items.Select(transfer => transfer.Id).ToArray());
        Assert.Null(transfers.NextCursor);
        Assert.Null(transfers.PreviousCursor);
    }

    [Fact]
    public async Task CancellingCompletedTransfer_Returns409()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        using var transferResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 100m, null, cancellationToken);
        var transfer = await FinanceApiClient.ReadAsync<TransferPayload>(transferResponse, cancellationToken);

        using var response = await ApiClient.CancelTransferAsync(transfer.Id, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("TransferCannotBeCancelled", problem.Code);
        var persistedTransfer = await ApiClient.ReadTransferAsync(transfer.Id, cancellationToken);
        Assert.Equal("Completed", persistedTransfer.Status);
    }

    [Fact]
    public async Task TransferLimits_ReturnDefaultsAndAcceptCustomLimits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var account = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var defaultLimits = await ApiClient.ReadTransferLimitsAsync(account.Id, cancellationToken);

        using var response = await ApiClient.SetTransferLimitsAsync(account.Id, 8000m, 10, 2000m, 4, cancellationToken);

        Assert.False(defaultLimits.IsCustom);
        Assert.Equal(new PeriodLimitPayload(5000m, 5), defaultLimits.Daytime);
        Assert.Equal(new PeriodLimitPayload(1000m, 3), defaultLimits.Nighttime);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var customLimits = await ApiClient.ReadTransferLimitsAsync(account.Id, cancellationToken);
        Assert.True(customLimits.IsCustom);
        Assert.Equal(new PeriodLimitPayload(8000m, 10), customLimits.Daytime);
        Assert.Equal(new PeriodLimitPayload(2000m, 4), customLimits.Nighttime);
    }
}
