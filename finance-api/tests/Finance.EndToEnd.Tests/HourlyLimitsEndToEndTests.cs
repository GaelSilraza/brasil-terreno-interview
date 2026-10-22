using System.Net;
using Finance.EndToEnd.Tests.Support;
using Finance.Testing;

namespace Finance.EndToEnd.Tests;

public sealed class HourlyLimitsEndToEndTests(EndToEndTestFixture fixture) : IAsyncLifetime
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
    public async Task DaytimeTransferAboveHourlyAmountLimit_Returns422HourlyAmountLimitExceeded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Carla Mendes", 100000m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        var limits = await ApiClient.ReadTransferLimitsAsync(sourceAccount.Id, cancellationToken);
        using var withinLimitResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 5000m, null, cancellationToken);

        using var response = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 0.01m, null, cancellationToken);

        Assert.Equal("Daytime", limits.CurrentPeriod);
        Assert.Equal(HttpStatusCode.Created, withinLimitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("HourlyAmountLimitExceeded", problem.Code);
        Assert.NotNull(problem.TransferId);
        var failedTransfer = await ApiClient.ReadTransferAsync(problem.TransferId.Value, cancellationToken);
        Assert.Equal("Failed", failedTransfer.Status);
        Assert.Equal("HourlyAmountLimitExceeded", failedTransfer.FailureCode);
        Assert.Equal(95000m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(5000m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task SixthAttemptInTheHour_Returns422AttemptLimitExceeded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Carla Mendes", 100m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        var allowedStatusCodes = new List<HttpStatusCode>();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var allowedResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 1m, null, cancellationToken);
            allowedStatusCodes.Add(allowedResponse.StatusCode);
        }

        using var response = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 1m, null, cancellationToken);

        Assert.All(allowedStatusCodes, statusCode => Assert.Equal(HttpStatusCode.Created, statusCode));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("AttemptLimitExceeded", problem.Code);
        Assert.NotNull(problem.TransferId);
        Assert.Equal(95m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        var limits = await ApiClient.ReadTransferLimitsAsync(sourceAccount.Id, cancellationToken);
        Assert.Equal(6, limits.Usage.AttemptsInLastHour);
        Assert.Equal(5m, limits.Usage.CompletedAmountInLastHour);
        Assert.Equal(0, limits.Usage.RemainingAttempts);
    }

    [Fact]
    public async Task NighttimeTransferAboveNightLimit_Returns422AndWithinLimitSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        fixture.Clock.SetUtcNow(TestClock.NighttimeInSaoPaulo);
        var sourceAccount = await ApiClient.OpenAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        var limits = await ApiClient.ReadTransferLimitsAsync(sourceAccount.Id, cancellationToken);

        using var aboveLimitResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 1000.01m, null, cancellationToken);
        using var withinLimitResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 1000m, null, cancellationToken);

        Assert.Equal("Nighttime", limits.CurrentPeriod);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, aboveLimitResponse.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(aboveLimitResponse, cancellationToken);
        Assert.Equal("HourlyAmountLimitExceeded", problem.Code);
        Assert.Equal(HttpStatusCode.Created, withinLimitResponse.StatusCode);
        var transfer = await FinanceApiClient.ReadAsync<TransferPayload>(withinLimitResponse, cancellationToken);
        Assert.Equal("Completed", transfer.Status);
        Assert.Equal(9000m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
        Assert.Equal(1000m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
    }
}
