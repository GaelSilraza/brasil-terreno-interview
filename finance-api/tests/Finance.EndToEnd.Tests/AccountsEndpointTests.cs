using System.Net;
using System.Text;
using Finance.EndToEnd.Tests.Support;
using Finance.Testing;

namespace Finance.EndToEnd.Tests;

public sealed class AccountsEndpointTests(EndToEndTestFixture fixture) : IAsyncLifetime
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
    public async Task CreatingAccount_Returns201WithLocationAndAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var documentNumber = FinanceApiClient.CreateDocumentNumber();

        using var response = await ApiClient.CreateAccountAsync("Fernanda Alves", documentNumber, 250m, 300m, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = await FinanceApiClient.ReadAsync<AccountPayload>(response, cancellationToken);
        Assert.Equal($"/api/accounts/{account.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Active", account.Status);
        Assert.Equal("Fernanda Alves", account.HolderName);
        Assert.Equal(documentNumber, account.DocumentNumber);
        Assert.Equal(250m, account.Balance);
        Assert.Equal(300m, account.OverdraftLimit);
        Assert.Equal(550m, account.AvailableAmount);
        Assert.Equal(0m, account.UsedOverdraft);
    }

    [Fact]
    public async Task CreatingAccountWithDuplicatedDocument_Returns409WithCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var documentNumber = FinanceApiClient.CreateDocumentNumber();
        using var firstResponse = await ApiClient.CreateAccountAsync("Fernanda Alves", documentNumber, 250m, 300m, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var response = await ApiClient.CreateAccountAsync("Gustavo Alves", documentNumber, 100m, 0m, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("DocumentNumberAlreadyExists", problem.Code);
        Assert.Equal("DocumentNumberAlreadyExists", problem.Title);
        Assert.Equal(409, problem.Status);
    }

    [Fact]
    public async Task CreatingAccountWithMaskedDocument_Returns201AndStoresOnlyDigits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await ApiClient.CreateAccountAsync("Fernanda Alves", "987.654.321-00", 250m, 300m, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = await FinanceApiClient.ReadAsync<AccountPayload>(response, cancellationToken);
        Assert.Equal("98765432100", account.DocumentNumber);
        using var getResponse = await ApiClient.GetAccountAsync(account.Id, cancellationToken);
        var persistedAccount = await FinanceApiClient.ReadAsync<AccountPayload>(getResponse, cancellationToken);
        Assert.Equal("98765432100", persistedAccount.DocumentNumber);
    }

    [Fact]
    public async Task CreatingAccountWithSameDocumentInOtherFormat_Returns409()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var firstResponse = await ApiClient.CreateAccountAsync("Fernanda Alves", "98765432100", 250m, 300m, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var response = await ApiClient.CreateAccountAsync("Gustavo Alves", "987.654.321-00", 100m, 0m, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("DocumentNumberAlreadyExists", problem.Code);
    }

    [Fact]
    public async Task CreatingAccountWithPartiallyMaskedDocument_Returns400InvalidFormat()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await ApiClient.CreateAccountAsync("Fernanda Alves", "987.65432100", 250m, 300m, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.FieldErrors);
        var fieldError = Assert.Single(problem.FieldErrors);
        Assert.Equal("documentNumber", fieldError.Field);
        Assert.Equal("InvalidFormat", fieldError.Code);
    }

    [Fact]
    public async Task CreatingAccountWithInvalidData_Returns400WithCamelCaseErrors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await ApiClient.CreateAccountAsync("Fe", "123", 100m, 0m, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.Errors);
        Assert.True(problem.Errors.ContainsKey("fullName"));
        Assert.True(problem.Errors.ContainsKey("documentNumber"));
        Assert.NotNull(problem.FieldErrors);
        Assert.Contains(problem.FieldErrors, fieldError => fieldError.Field == "fullName" && fieldError.Code == "InvalidLength");
        Assert.Contains(problem.FieldErrors, fieldError => fieldError.Field == "documentNumber" && fieldError.Code == "InvalidFormat");
    }

    [Fact]
    public async Task SendingMalformedJson_Returns400ValidationFailed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var content = new StringContent("{ \"fullName\": 1", Encoding.UTF8, "application/json");

        using var response = await fixture.HttpClient.PostAsync("/api/accounts", content, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
    }

    [Fact]
    public async Task GettingUnknownAccount_Returns404WithCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await ApiClient.GetAccountAsync(Guid.CreateVersion7(), cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("AccountNotFound", problem.Code);
    }

    [Fact]
    public async Task UpdatingOverdraftLimitOfUnknownAccount_Returns404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await ApiClient.UpdateOverdraftLimitAsync(Guid.CreateVersion7(), 1500m, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("AccountNotFound", problem.Code);
    }

    [Fact]
    public async Task ReceivingWhileUsingOverdraft_StatementShowsOverdraftCoveredFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overdraftAccount = await ApiClient.OpenAccountAsync("Ana Souza", 0m, 1000m, cancellationToken);
        var receivingAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        var payingAccount = await ApiClient.OpenAccountAsync("Carla Mendes", 5000m, 0m, cancellationToken);
        using var overdraftResponse = await ApiClient.PostTransferAsync(overdraftAccount.Id, receivingAccount.Id, 800m, null, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, overdraftResponse.StatusCode);
        Assert.Equal(-800m, await ApiClient.GetBalanceAsync(overdraftAccount.Id, cancellationToken));

        using var creditResponse = await ApiClient.PostTransferAsync(payingAccount.Id, overdraftAccount.Id, 1000m, null, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, creditResponse.StatusCode);
        Assert.Equal(200m, await ApiClient.GetBalanceAsync(overdraftAccount.Id, cancellationToken));
        var entries = await ApiClient.ListEntriesAsync(overdraftAccount.Id, null, null, null, cancellationToken);
        Assert.Equal(2, entries.Items.Count);
        Assert.Null(entries.NextCursor);
        Assert.Equal("Credit", entries.Items[0].EntryType);
        Assert.Equal(1000m, entries.Items[0].Amount);
        Assert.Equal(200m, entries.Items[0].BalanceAfter);
        Assert.Equal("Carla Mendes", entries.Items[0].CounterpartHolderName);
        Assert.Equal("Debit", entries.Items[1].EntryType);
        Assert.Equal(800m, entries.Items[1].Amount);
        Assert.Equal(-800m, entries.Items[1].BalanceAfter);
        Assert.Equal("Bruno Lima", entries.Items[1].CounterpartHolderName);
    }
}
