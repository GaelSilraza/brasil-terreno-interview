using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Application.UseCases.ListAccounts;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Tests.Support;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Domain;
using Finance.Testing;

namespace Finance.Modules.Accounts.Tests;

public sealed class AccountSearchTests(AccountsTestFixture fixture) : IAsyncLifetime
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
    public async Task WalkingEveryPageForward_ReturnsEveryAccountExactlyOnceInOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fullNames = Enumerable.Range(0, 57).Select(_ => "Titular " + AccountsScenario.RandomUppercaseLetters(8)).ToList();
        await AccountsScenario.CreateAccountsAsync(environment, fullNames, cancellationToken);

        var accounts = await AccountsScenario.WalkEveryPageForwardAsync(environment, null, 10, cancellationToken);

        Assert.Equal(57, accounts.Select(account => account.Id).Distinct().Count());
        Assert.Equal(57, accounts.Count);
        var expectedOrder = accounts
            .OrderBy(account => account.HolderName, StringComparer.Ordinal)
            .ThenBy(account => account.PersonId)
            .Select(account => account.Id);
        Assert.Equal(expectedOrder, accounts.Select(account => account.Id));
    }

    [Fact]
    public async Task WalkingBackwardWithPreviousCursor_ReturnsThePreviousPageInDisplayOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fullNames = Enumerable.Range(0, 25).Select(_ => "Titular " + AccountsScenario.RandomUppercaseLetters(8)).ToList();
        await AccountsScenario.CreateAccountsAsync(environment, fullNames, cancellationToken);
        var firstPage = await ListAsync(new ListAccountsQuery(null, null, null, 10), cancellationToken);
        var secondPage = await ListAsync(new ListAccountsQuery(null, firstPage.NextCursor, "next", 10), cancellationToken);

        var previousPage = await ListAsync(new ListAccountsQuery(null, secondPage.PreviousCursor, "previous", 10), cancellationToken);

        Assert.Equal(firstPage.Items.Select(account => account.Id), previousPage.Items.Select(account => account.Id));
        Assert.Null(previousPage.PreviousCursor);
        Assert.NotNull(previousPage.NextCursor);
        Assert.DoesNotContain(secondPage.Items, account => firstPage.Items.Any(firstPageAccount => firstPageAccount.Id == account.Id));
    }

    [Fact]
    public async Task AccountsWithTheSameName_AreAllReachableAcrossPageBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fullNames = Enumerable.Repeat("Mesmo Nome", 25).ToList();
        await AccountsScenario.CreateAccountsAsync(environment, fullNames, cancellationToken);

        var accounts = await AccountsScenario.WalkEveryPageForwardAsync(environment, null, 7, cancellationToken);

        Assert.Equal(25, accounts.Count);
        Assert.Equal(25, accounts.Select(account => account.Id).Distinct().Count());
        Assert.All(accounts, account => Assert.Equal("Mesmo Nome", account.HolderName));
        Assert.Equal(accounts.OrderBy(account => account.PersonId).Select(account => account.Id), accounts.Select(account => account.Id));
    }

    [Fact]
    public async Task SearchingByNameFragment_IsCaseInsensitiveAndMatchesAnywhere()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await AccountsScenario.CreateAccountsAsync(environment, ["Maria Clara Souza", "João Souza", "Pedro Lima"], cancellationToken);

        var souzaPage = await ListAsync(new ListAccountsQuery("souza", null, null, null), cancellationToken);
        var claraPage = await ListAsync(new ListAccountsQuery("CLARA", null, null, null), cancellationToken);

        Assert.Equal(
            ["João Souza", "Maria Clara Souza"],
            souzaPage.Items.Select(account => account.HolderName).Order(StringComparer.Ordinal));
        Assert.Equal("Maria Clara Souza", Assert.Single(claraPage.Items).HolderName);
    }

    [Fact]
    public async Task SearchingByDocumentPrefix_AcceptsMaskedAndUnmaskedInput()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await AccountsScenario.CreateAccountAsync(environment, "Ana Souza", "12345678901", 100m, 0m, AccountStatus.Active, cancellationToken);
        await AccountsScenario.CreateAccountAsync(environment, "Bruno Lima", "12345000000", 100m, 0m, AccountStatus.Active, cancellationToken);
        await AccountsScenario.CreateAccountAsync(environment, "Carla Mendes", "99999999999", 100m, 0m, AccountStatus.Active, cancellationToken);

        var unmaskedPrefixPage = await ListAsync(new ListAccountsQuery("12345", null, null, null), cancellationToken);
        var maskedPrefixPage = await ListAsync(new ListAccountsQuery("123.45", null, null, null), cancellationToken);
        var maskedDocumentPage = await ListAsync(new ListAccountsQuery("123.456.789-01", null, null, null), cancellationToken);

        Assert.Equal(["Ana Souza", "Bruno Lima"], unmaskedPrefixPage.Items.Select(account => account.HolderName));
        Assert.Equal(["Ana Souza", "Bruno Lima"], maskedPrefixPage.Items.Select(account => account.HolderName));
        Assert.Equal("12345678901", Assert.Single(maskedDocumentPage.Items).DocumentNumber);
    }

    [Fact]
    public async Task SearchingWithLikeWildcards_TreatsThemAsLiteralText()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await AccountsScenario.CreateAccountsAsync(environment, ["Ana_Lima", "Ana Lima"], cancellationToken);

        var underscorePage = await ListAsync(new ListAccountsQuery("Ana_", null, null, null), cancellationToken);
        var percentPage = await ListAsync(new ListAccountsQuery("%", null, null, null), cancellationToken);

        Assert.Equal("Ana_Lima", Assert.Single(underscorePage.Items).HolderName);
        Assert.Empty(percentPage.Items);
    }

    [Fact]
    public async Task AccountBeyondTheFirstPage_IsFoundBySearch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fullNames = Enumerable.Range(1, 149).Select(number => $"Pessoa {number:D3}").Append("Zuleica Rarissima").ToList();
        await AccountsScenario.CreateAccountsAsync(environment, fullNames, cancellationToken);

        var firstPage = await ListAsync(new ListAccountsQuery(null, null, null, null), cancellationToken);
        var searchPage = await ListAsync(new ListAccountsQuery("Rarissima", null, null, null), cancellationToken);

        Assert.Equal(20, firstPage.Items.Count);
        Assert.NotNull(firstPage.NextCursor);
        Assert.DoesNotContain(firstPage.Items, account => account.HolderName == "Zuleica Rarissima");
        Assert.Equal("Zuleica Rarissima", searchPage.Items[0].HolderName);
    }

    [Fact]
    public async Task InvalidCursorOrLimit_ReturnsNormalizedFieldErrors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var invalidCursorResult = await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery(null, "not-a-cursor", null, null), cancellationToken);
        var invalidLimitResult = await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery(null, null, null, 0), cancellationToken);
        var invalidDirectionResult = await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery(null, null, "sideways", null), cancellationToken);

        AssertSingleFieldError(invalidCursorResult, "cursor", FieldErrorCodes.InvalidValue);
        AssertSingleFieldError(invalidLimitResult, "limit", FieldErrorCodes.OutOfRange);
        AssertSingleFieldError(invalidDirectionResult, "direction", FieldErrorCodes.InvalidOption);
    }

    private static void AssertSingleFieldError(Either<Error, CursorPage<AccountResponse>> result, string field, string code)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(Error.ValidationFailedCode, result.Failure.Code);
        var fieldError = Assert.Single(result.Failure.FieldErrors);
        Assert.Equal(field, fieldError.Field);
        Assert.Equal(code, fieldError.Code);
    }

    private async Task<CursorPage<AccountResponse>> ListAsync(ListAccountsQuery query, CancellationToken cancellationToken)
    {
        var pageResult = await AccountsScenario.ListAccountsAsync(environment, query, cancellationToken);
        return pageResult.Success;
    }
}
