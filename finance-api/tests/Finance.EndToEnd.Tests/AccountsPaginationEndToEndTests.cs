using System.Globalization;
using System.Net;
using Finance.EndToEnd.Tests.Support;
using Finance.Testing;

namespace Finance.EndToEnd.Tests;

public sealed class AccountsPaginationEndToEndTests(EndToEndTestFixture fixture) : IAsyncLifetime
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
    public async Task WalkingTheAccountListForwardAndBack_ReturnsEveryAccountOnceInOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var holderNames = Enumerable.Range(1, 20)
            .Select(number => $"Pessoa {number.ToString("00", CultureInfo.InvariantCulture)}")
            .Concat(["Pessoa Repetida", "Pessoa Repetida", "Pessoa Repetida"])
            .ToList();
        var createdAccounts = new List<AccountPayload>();

        foreach (var holderName in holderNames)
        {
            createdAccounts.Add(await ApiClient.OpenAccountAsync(holderName, 0m, 0m, cancellationToken));
        }

        var expectedIds = createdAccounts
            .OrderBy(account => account.HolderName, StringComparer.Ordinal)
            .ThenBy(account => account.PersonId.ToString(), StringComparer.Ordinal)
            .Select(account => account.Id)
            .ToList();

        var firstPage = await ApiClient.ListAccountsAsync(null, null, null, 10, cancellationToken);
        var secondPage = await ApiClient.ListAccountsAsync(null, firstPage.NextCursor, "next", 10, cancellationToken);
        var lastPage = await ApiClient.ListAccountsAsync(null, secondPage.NextCursor, "next", 10, cancellationToken);
        var secondPageAgain = await ApiClient.ListAccountsAsync(null, lastPage.PreviousCursor, "previous", 10, cancellationToken);

        Assert.Equal([10, 10, 3], new[] { firstPage.Items.Count, secondPage.Items.Count, lastPage.Items.Count });
        var walkedIds = firstPage.Items.Concat(secondPage.Items).Concat(lastPage.Items).Select(account => account.Id).ToList();
        Assert.Equal(23, walkedIds.Distinct().Count());
        Assert.Equal(expectedIds, walkedIds);
        Assert.Null(firstPage.PreviousCursor);
        Assert.NotNull(firstPage.NextCursor);
        Assert.NotNull(secondPage.PreviousCursor);
        Assert.NotNull(secondPage.NextCursor);
        Assert.NotNull(lastPage.PreviousCursor);
        Assert.Null(lastPage.NextCursor);
        Assert.Equal(secondPage.Items.Select(account => account.Id), secondPageAgain.Items.Select(account => account.Id));
        Assert.NotNull(secondPageAgain.PreviousCursor);
        Assert.NotNull(secondPageAgain.NextCursor);
    }

    [Fact]
    public async Task SearchingAccounts_ByNameFragmentAndByMaskedOrUnmaskedDocument()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var helenaResponse = await ApiClient.CreateAccountAsync("Helena Prado", "12345678901", 100m, 0m, cancellationToken);
        var helena = await FinanceApiClient.ReadAsync<AccountPayload>(helenaResponse, cancellationToken);
        using var igorResponse = await ApiClient.CreateAccountAsync("Igor Prado", "12399999999", 100m, 0m, cancellationToken);
        var igor = await FinanceApiClient.ReadAsync<AccountPayload>(igorResponse, cancellationToken);
        await ApiClient.OpenAccountAsync("Juliana Costa", 100m, 0m, cancellationToken);

        var byNameFragment = await ApiClient.ListAccountsAsync("prado", null, null, null, cancellationToken);
        var byMaskedDocumentPrefix = await ApiClient.ListAccountsAsync("123.45", null, null, null, cancellationToken);
        var byFullDocument = await ApiClient.ListAccountsAsync("12345678901", null, null, null, cancellationToken);

        Assert.Equal([helena.Id, igor.Id], byNameFragment.Items.Select(account => account.Id).ToArray());
        Assert.Equal(helena.Id, Assert.Single(byMaskedDocumentPrefix.Items).Id);
        Assert.Equal(helena.Id, Assert.Single(byFullDocument.Items).Id);
    }

    [Fact]
    public async Task AccountThatIsNotOnTheFirstPage_IsReachableBySearch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var number = 1; number <= 30; number++)
        {
            await ApiClient.OpenAccountAsync($"Conta {number.ToString("00", CultureInfo.InvariantCulture)}", 0m, 0m, cancellationToken);
        }

        var lastAccount = await ApiClient.OpenAccountAsync("Zélia Final", 0m, 0m, cancellationToken);

        var firstPage = await ApiClient.ListAccountsAsync(null, null, null, 10, cancellationToken);
        var searchResult = await ApiClient.ListAccountsAsync("Zélia", null, null, 10, cancellationToken);

        Assert.Equal(10, firstPage.Items.Count);
        Assert.DoesNotContain(firstPage.Items, account => account.Id == lastAccount.Id);
        Assert.NotNull(firstPage.NextCursor);
        var foundAccount = Assert.Single(searchResult.Items);
        Assert.Equal(lastAccount.Id, foundAccount.Id);
        Assert.Equal("Zélia Final", foundAccount.HolderName);
        Assert.Null(searchResult.NextCursor);
    }

    [Fact]
    public async Task InvalidPaginationParameters_Return400WithNormalizedFieldErrors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var limitResponse = await ApiClient.GetAccountsAsync(null, null, null, 0, cancellationToken);
        using var cursorResponse = await ApiClient.GetAccountsAsync(null, "abc", null, null, cancellationToken);
        using var directionResponse = await ApiClient.GetAccountsAsync(null, null, "up", null, cancellationToken);
        using var missingAccountIdResponse = await ApiClient.GetTransfersAsync(null, null, null, null, cancellationToken);

        await AssertFieldErrorAsync(limitResponse, "limit", "OutOfRange", cancellationToken);
        await AssertFieldErrorAsync(cursorResponse, "cursor", "InvalidValue", cancellationToken);
        await AssertFieldErrorAsync(directionResponse, "direction", "InvalidOption", cancellationToken);
        await AssertFieldErrorAsync(missingAccountIdResponse, "accountId", "Required", cancellationToken);
    }

    [Fact]
    public async Task StatementAndTransferHistory_PageByCursorNewestFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 1000m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);
        await ApiClient.AllowManyTransfersPerHourAsync(sourceAccount.Id, cancellationToken);
        var transferIds = new List<Guid>();

        for (var number = 1; number <= 7; number++)
        {
            using var transferResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, number, null, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, transferResponse.StatusCode);
            var transfer = await FinanceApiClient.ReadAsync<TransferPayload>(transferResponse, cancellationToken);
            transferIds.Add(transfer.Id);
        }

        var newestFirstTransferIds = Enumerable.Reverse(transferIds).ToList();

        var entryPages = await ReadAllEntryPagesAsync(sourceAccount.Id, cancellationToken);
        var transferPages = await ReadAllTransferPagesAsync(sourceAccount.Id, cancellationToken);

        Assert.Equal([3, 3, 2], entryPages.Select(page => page.Items.Count).ToArray());
        Assert.Equal([3, 3, 1], transferPages.Select(page => page.Items.Count).ToArray());
        var entries = entryPages.SelectMany(page => page.Items).ToList();
        var transfers = transferPages.SelectMany(page => page.Items).ToList();
        Assert.Equal(8, entries.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(7, transfers.Select(transfer => transfer.Id).Distinct().Count());
        Assert.Equal(newestFirstTransferIds, entries.Take(7).Select(entry => entry.TransferId!.Value).ToList());
        Assert.Equal(newestFirstTransferIds, transfers.Select(transfer => transfer.Id).ToList());
        Assert.All(entries.Take(7), entry => Assert.Equal("Debit", entry.EntryType));
        Assert.Equal([7m, 6m, 5m, 4m, 3m, 2m, 1m], entries.Take(7).Select(entry => entry.Amount).ToArray());
        var openingEntry = entries[^1];
        Assert.Null(openingEntry.TransferId);
        Assert.Equal("Credit", openingEntry.EntryType);
        Assert.Equal(1000m, openingEntry.BalanceAfter);
        Assert.Null(entryPages[^1].NextCursor);
        Assert.Null(transferPages[^1].NextCursor);
    }

    private static async Task AssertFieldErrorAsync(
        HttpResponseMessage response,
        string expectedField,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.FieldErrors);
        Assert.Contains(problem.FieldErrors, fieldError => fieldError.Field == expectedField && fieldError.Code == expectedCode);
    }

    private async Task<List<CursorPagePayload<AccountEntryPayload>>> ReadAllEntryPagesAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var pages = new List<CursorPagePayload<AccountEntryPayload>>();
        string? cursor = null;

        do
        {
            var page = await ApiClient.ListEntriesAsync(accountId, cursor, "next", 3, cancellationToken);
            pages.Add(page);
            cursor = page.NextCursor;
        }
        while (cursor is not null && pages.Count < 10);

        return pages;
    }

    private async Task<List<CursorPagePayload<TransferPayload>>> ReadAllTransferPagesAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var pages = new List<CursorPagePayload<TransferPayload>>();
        string? cursor = null;

        do
        {
            var page = await ApiClient.ListTransfersAsync(accountId, cursor, "next", 3, cancellationToken);
            pages.Add(page);
            cursor = page.NextCursor;
        }
        while (cursor is not null && pages.Count < 10);

        return pages;
    }
}
