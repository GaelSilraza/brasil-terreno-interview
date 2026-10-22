using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Tests.Support;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Tests;

public sealed class FundsTransferTests(AccountsTestFixture fixture) : IAsyncLifetime
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
    public async Task TransferWithSufficientBalance_DebitsSourceAndCreditsDestination()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Ana Souza", 500m, 1000m, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 100m, cancellationToken);

        Assert.True(transferResult.IsSuccessful);
        var sourceAccount = await AccountsScenario.GetAccountAsync(environment, sourceAccountId, cancellationToken);
        var destinationAccount = await AccountsScenario.GetAccountAsync(environment, destinationAccountId, cancellationToken);
        Assert.Equal(400m, sourceAccount.Balance);
        Assert.Equal(10100m, destinationAccount.Balance);
        var sourceEntry = Assert.Single(await AccountsScenario.ListEntriesAsync(environment, sourceAccountId, cancellationToken), entry => entry.TransferId is not null);
        var destinationEntry = Assert.Single(await AccountsScenario.ListEntriesAsync(environment, destinationAccountId, cancellationToken), entry => entry.TransferId is not null);
        Assert.Equal(AccountEntryType.Debit, sourceEntry.EntryType);
        Assert.Equal(100m, sourceEntry.Amount);
        Assert.Equal(400m, sourceEntry.BalanceAfter);
        Assert.Equal(destinationAccountId, sourceEntry.CounterpartAccountId);
        Assert.Equal(AccountEntryType.Credit, destinationEntry.EntryType);
        Assert.Equal(100m, destinationEntry.Amount);
        Assert.Equal(10100m, destinationEntry.BalanceAfter);
        Assert.Equal(sourceAccountId, destinationEntry.CounterpartAccountId);
        Assert.Equal(sourceEntry.TransferId, destinationEntry.TransferId);
    }

    [Fact]
    public async Task TransferUsingOverdraft_LeavesSourceBalanceNegative()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Ana Souza", 500m, 1000m, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 1000m, cancellationToken);

        Assert.True(transferResult.IsSuccessful);
        var sourceAccount = await AccountsScenario.GetAccountAsync(environment, sourceAccountId, cancellationToken);
        Assert.Equal(-500m, sourceAccount.Balance);
        Assert.Equal(500m, sourceAccount.UsedOverdraft);
        Assert.Equal(500m, sourceAccount.AvailableAmount);
    }

    [Fact]
    public async Task TransferOfExactlyBalancePlusOverdraft_IsAllowed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Ana Souza", 500m, 1000m, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 1500m, cancellationToken);

        Assert.True(transferResult.IsSuccessful);
        var sourceAccount = await AccountsScenario.GetAccountAsync(environment, sourceAccountId, cancellationToken);
        Assert.Equal(-1000m, sourceAccount.Balance);
        Assert.Equal(0m, sourceAccount.AvailableAmount);
    }

    [Fact]
    public async Task TransferAboveBalancePlusOverdraft_ReturnsInsufficientFundsAndChangesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Ana Souza", 500m, 1000m, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 1500.01m, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("InsufficientFunds", transferResult.Failure.Code);
        await AssertBalancesUnchangedAsync(sourceAccountId, 500m, destinationAccountId, 10000m, cancellationToken);
    }

    [Fact]
    public async Task ReceivingWhileUsingOverdraft_CoversOverdraftFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Bruno Lima", -800m, 1000m, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 1000m, cancellationToken);

        Assert.True(transferResult.IsSuccessful);
        var destinationAccount = await AccountsScenario.GetAccountAsync(environment, destinationAccountId, cancellationToken);
        Assert.Equal(200m, destinationAccount.Balance);
        Assert.Equal(0m, destinationAccount.UsedOverdraft);
        var creditEntry = Assert.Single(await AccountsScenario.ListEntriesAsync(environment, destinationAccountId, cancellationToken), entry => entry.TransferId is not null);
        Assert.Equal(AccountEntryType.Credit, creditEntry.EntryType);
        Assert.Equal(200m, creditEntry.BalanceAfter);
    }

    [Fact]
    public async Task TransferFromBlockedAccount_ReturnsSourceAccountInactive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await AccountsScenario.CreateAccountAsync(environment, "Daniel Rocha", AccountsScenario.UniqueDocumentNumber(), 300m, 0m, AccountStatus.Blocked, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 100m, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("SourceAccountInactive", transferResult.Failure.Code);
        await AssertBalancesUnchangedAsync(sourceAccountId, 300m, destinationAccountId, 10000m, cancellationToken);
    }

    [Fact]
    public async Task TransferToInactiveAccount_ReturnsDestinationAccountInactive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);
        var destinationAccountId = await AccountsScenario.CreateAccountAsync(environment, "Elisa Martins", AccountsScenario.UniqueDocumentNumber(), 2000m, 500m, AccountStatus.Inactive, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 100m, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("DestinationAccountInactive", transferResult.Failure.Code);
        await AssertBalancesUnchangedAsync(sourceAccountId, 10000m, destinationAccountId, 2000m, cancellationToken);
    }

    [Fact]
    public async Task CreditAboveTheMaximumBalance_ReturnsDestinationBalanceLimitExceededAndChangesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var nearlyFullBalance = Account.MaximumBalance - 50m;
        var sourceAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Bruno Lima", nearlyFullBalance, 0m, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 50.01m, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("DestinationBalanceLimitExceeded", transferResult.Failure.Code);
        await AssertBalancesUnchangedAsync(sourceAccountId, 10000m, destinationAccountId, nearlyFullBalance, cancellationToken);
        var creditUpToTheMaximum = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 50m, cancellationToken);
        Assert.True(creditUpToTheMaximum.IsSuccessful);
        var destinationAccount = await AccountsScenario.GetAccountAsync(environment, destinationAccountId, cancellationToken);
        Assert.Equal(Account.MaximumBalance, destinationAccount.Balance);
    }

    [Fact]
    public async Task TransferWithUnknownAccount_ReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var existingAccountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);

        var unknownSourceResult = await AccountsScenario.TransferFundsAsync(environment, Guid.CreateVersion7(), existingAccountId, 100m, cancellationToken);
        var unknownDestinationResult = await AccountsScenario.TransferFundsAsync(environment, existingAccountId, Guid.CreateVersion7(), 100m, cancellationToken);

        Assert.True(unknownSourceResult.IsFailure);
        Assert.Equal("SourceAccountNotFound", unknownSourceResult.Failure.Code);
        Assert.True(unknownDestinationResult.IsFailure);
        Assert.Equal("DestinationAccountNotFound", unknownDestinationResult.Failure.Code);
        var existingAccount = await AccountsScenario.GetAccountAsync(environment, existingAccountId, cancellationToken);
        Assert.Equal(10000m, existingAccount.Balance);
    }

    [Fact]
    public async Task TransferToSameAccount_ReturnsSameAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accountId = await CreateActiveAccountAsync("Carla Mendes", 10000m, 0m, cancellationToken);

        var transferResult = await AccountsScenario.TransferFundsAsync(environment, accountId, accountId, 100m, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("SameAccount", transferResult.Failure.Code);
        var account = await AccountsScenario.GetAccountAsync(environment, accountId, cancellationToken);
        Assert.Equal(10000m, account.Balance);
    }

    private Task<Guid> CreateActiveAccountAsync(string fullName, decimal balance, decimal overdraftLimit, CancellationToken cancellationToken)
    {
        return AccountsScenario.CreateAccountAsync(environment, fullName, AccountsScenario.UniqueDocumentNumber(), balance, overdraftLimit, AccountStatus.Active, cancellationToken);
    }

    private async Task AssertBalancesUnchangedAsync(
        Guid sourceAccountId,
        decimal expectedSourceBalance,
        Guid destinationAccountId,
        decimal expectedDestinationBalance,
        CancellationToken cancellationToken)
    {
        var sourceAccount = await AccountsScenario.GetAccountAsync(environment, sourceAccountId, cancellationToken);
        var destinationAccount = await AccountsScenario.GetAccountAsync(environment, destinationAccountId, cancellationToken);
        Assert.Equal(expectedSourceBalance, sourceAccount.Balance);
        Assert.Equal(expectedDestinationBalance, destinationAccount.Balance);
        await using var scope = environment.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var transferEntryCount = await dbContext.Set<AccountEntry>().CountAsync(entry => entry.TransferId != null, cancellationToken);
        Assert.Equal(0, transferEntryCount);
    }
}
