using System.Net;
using Finance.EndToEnd.Tests.Support;
using Finance.Testing;

namespace Finance.EndToEnd.Tests;

public sealed class ConcurrencyEndToEndTests(EndToEndTestFixture fixture) : IAsyncLifetime
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
    public async Task SimultaneousHttpTransfersSharingTheSameBalance_OnlyOneCompletes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 1000m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        var responses = await Task.WhenAll(
            ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 700m, null, cancellationToken),
            ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 700m, null, cancellationToken));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            var rejectedResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.UnprocessableEntity);
            var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(rejectedResponse, cancellationToken);
            Assert.Equal("InsufficientFunds", problem.Code);
            Assert.Equal(300m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
            Assert.Equal(700m, await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken));
        }
        finally
        {
            DisposeAll(responses);
        }
    }

    [Fact]
    public async Task ManySimultaneousHttpTransfers_KeepBalancesConsistent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 1000m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 250m, 0m, cancellationToken);
        await ApiClient.AllowManyTransfersPerHourAsync(sourceAccount.Id, cancellationToken);
        var transferRequests = Enumerable.Range(0, 10)
            .Select(_ => ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 300m, null, cancellationToken))
            .ToList();

        var responses = await Task.WhenAll(transferRequests);

        try
        {
            Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(7, responses.Count(response => response.StatusCode == HttpStatusCode.UnprocessableEntity));
            var sourceBalance = await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken);
            var destinationBalance = await ApiClient.GetBalanceAsync(destinationAccount.Id, cancellationToken);
            Assert.Equal(100m, sourceBalance);
            Assert.Equal(250m + 900m, destinationBalance);
            Assert.Equal(1000m + 250m, sourceBalance + destinationBalance);
        }
        finally
        {
            DisposeAll(responses);
        }
    }

    private static void DisposeAll(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
