using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Application.UseCases.ListAccountEntries;
using Finance.Modules.Accounts.Application.UseCases.ListAccounts;
using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Tests.Support;
using Finance.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Tests;

public sealed class AccountQueriesTests(AccountsTestFixture fixture) : IAsyncLifetime
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
    public async Task ListingAccounts_ReturnsPagedAccountsOrderedByHolderName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateActiveAccountAsync("Carla Mendes", cancellationToken);
        await CreateActiveAccountAsync("Ana Souza", cancellationToken);
        await CreateActiveAccountAsync("Bruno Lima", cancellationToken);

        var firstPageResult = await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery(null, null, null, 2), cancellationToken);
        var firstPage = firstPageResult.Success;
        var secondPageResult = await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery(null, firstPage.NextCursor, "next", 2), cancellationToken);
        var secondPage = secondPageResult.Success;

        Assert.Equal(["Ana Souza", "Bruno Lima"], firstPage.Items.Select(account => account.HolderName));
        Assert.NotNull(firstPage.NextCursor);
        Assert.Null(firstPage.PreviousCursor);
        Assert.Equal(["Carla Mendes"], secondPage.Items.Select(account => account.HolderName));
        Assert.Null(secondPage.NextCursor);
        Assert.NotNull(secondPage.PreviousCursor);
    }

    [Fact]
    public async Task ListingEntries_ReturnsStatementNewestFirstWithCounterpartName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Ana Souza", cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Bruno Lima", cancellationToken);
        environment.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 100m, cancellationToken);
        environment.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 50m, cancellationToken);

        var entries = await AccountsScenario.ListEntriesAsync(environment, sourceAccountId, cancellationToken);

        Assert.Equal(3, entries.Count);
        Assert.Equal(50m, entries[0].Amount);
        Assert.Equal(850m, entries[0].BalanceAfter);
        Assert.Equal(100m, entries[1].Amount);
        Assert.Equal(900m, entries[1].BalanceAfter);
        Assert.True(entries[0].CreatedAt > entries[1].CreatedAt);
        Assert.All(entries.Take(2), entry => Assert.Equal("Bruno Lima", entry.CounterpartHolderName));
        Assert.All(entries.Take(2), entry => Assert.Equal(AccountEntryType.Debit, entry.EntryType));
        var openingEntry = entries[2];
        Assert.Null(openingEntry.TransferId);
        Assert.Null(openingEntry.CounterpartAccountId);
        Assert.Null(openingEntry.CounterpartHolderName);
        Assert.Equal(AccountEntryType.Credit, openingEntry.EntryType);
        Assert.Equal(1000m, openingEntry.BalanceAfter);
    }

    [Fact]
    public async Task ListingEntriesAcrossPages_ReturnsTheWholeStatementNewestFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Ana Souza", cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Bruno Lima", cancellationToken);
        var amounts = Enumerable.Range(1, 12).Select(amount => (decimal)amount).ToList();
        environment.TimeProvider.Advance(TimeSpan.FromSeconds(1));

        foreach (var amount in amounts)
        {
            await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, amount, cancellationToken);
            environment.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        }

        var entries = new List<AccountEntryResponse>();
        var pageCount = 0;
        string? cursor = null;

        do
        {
            var pageResult = await AccountsScenario.ListEntriesPageAsync(environment, new ListAccountEntriesQuery(sourceAccountId, cursor, "next", 5), cancellationToken);
            entries.AddRange(pageResult.Success.Items);
            cursor = pageResult.Success.NextCursor;
            pageCount++;
        }
        while (cursor is not null);

        Assert.Equal(3, pageCount);
        Assert.Equal(13, entries.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(amounts.AsEnumerable().Reverse(), entries.Take(12).Select(entry => entry.Amount));
        Assert.Null(entries[^1].TransferId);
        Assert.Equal(entries.OrderByDescending(entry => entry.CreatedAt).Select(entry => entry.Id), entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task FindingTransferParticipants_ReturnsHolderNamesAndActiveFlag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var activeAccountId = await CreateActiveAccountAsync("Ana Souza", cancellationToken);
        var blockedAccountId = await AccountsScenario.CreateAccountAsync(environment, "Daniel Rocha", AccountsScenario.UniqueDocumentNumber(), 300m, 0m, AccountStatus.Blocked, cancellationToken);

        await using var scope = environment.CreateScope();
        var accountsModule = scope.ServiceProvider.GetRequiredService<IAccountsModule>();
        var participantsResult = await accountsModule.FindTransferParticipantsAsync(activeAccountId, blockedAccountId, cancellationToken);

        Assert.True(participantsResult.IsSuccessful);
        var participants = participantsResult.Success;
        Assert.Equal(activeAccountId, participants.Source.AccountId);
        Assert.Equal("Ana Souza", participants.Source.HolderName);
        Assert.True(participants.Source.IsActive);
        Assert.Equal(blockedAccountId, participants.Destination.AccountId);
        Assert.Equal("Daniel Rocha", participants.Destination.HolderName);
        Assert.False(participants.Destination.IsActive);
    }

    private Task<Guid> CreateActiveAccountAsync(string fullName, CancellationToken cancellationToken)
    {
        return AccountsScenario.CreateAccountAsync(environment, fullName, AccountsScenario.UniqueDocumentNumber(), 1000m, 0m, AccountStatus.Active, cancellationToken);
    }
}
