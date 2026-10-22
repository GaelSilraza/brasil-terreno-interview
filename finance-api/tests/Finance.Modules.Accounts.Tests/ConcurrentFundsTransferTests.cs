using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Tests.Support;
using Finance.Shared.Domain;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Tests;

public sealed class ConcurrentFundsTransferTests(AccountsTestFixture fixture) : IAsyncLifetime
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
    public async Task SimultaneousTransfersSharingTheSameBalance_OnlyOneSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Ana Souza", 1000m, 0m, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        var transferResults = await Task.WhenAll(
            StartTransfer(sourceAccountId, destinationAccountId, 700m, cancellationToken),
            StartTransfer(sourceAccountId, destinationAccountId, 700m, cancellationToken));

        Assert.Single(transferResults, transferResult => transferResult.IsSuccessful);
        var failedResult = Assert.Single(transferResults, transferResult => transferResult.IsFailure);
        Assert.Equal("InsufficientFunds", failedResult.Failure.Code);
        var sourceAccount = await AccountsScenario.GetAccountAsync(environment, sourceAccountId, cancellationToken);
        var destinationAccount = await AccountsScenario.GetAccountAsync(environment, destinationAccountId, cancellationToken);
        Assert.Equal(300m, sourceAccount.Balance);
        Assert.Equal(700m, destinationAccount.Balance);
    }

    [Fact]
    public async Task ManySimultaneousTransfers_NeverExceedBalancePlusOverdraft()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateActiveAccountAsync("Ana Souza", 1000m, 500m, cancellationToken);
        var destinationAccountId = await CreateActiveAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        var transferResults = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => StartTransfer(sourceAccountId, destinationAccountId, 100m, cancellationToken)));

        Assert.Equal(15, transferResults.Count(transferResult => transferResult.IsSuccessful));
        Assert.All(
            transferResults.Where(transferResult => transferResult.IsFailure),
            transferResult => Assert.Equal("InsufficientFunds", transferResult.Failure.Code));
        var sourceAccount = await AccountsScenario.GetAccountAsync(environment, sourceAccountId, cancellationToken);
        Assert.Equal(-500m, sourceAccount.Balance);
        await using var scope = environment.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var debitTotal = await dbContext.Set<AccountEntry>()
            .Where(entry => entry.AccountId == sourceAccountId && entry.EntryType == AccountEntryType.Debit)
            .SumAsync(entry => entry.Amount, cancellationToken);
        Assert.Equal(1500m, debitTotal);
    }

    [Fact]
    public async Task CrossingTransfersInParallel_CompleteWithoutDeadlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstAccountId = await CreateActiveAccountAsync("Ana Souza", 10000m, 0m, cancellationToken);
        var secondAccountId = await CreateActiveAccountAsync("Bruno Lima", 10000m, 0m, cancellationToken);

        var transferResults = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(transferNumber => transferNumber % 2 == 0
                ? StartTransfer(firstAccountId, secondAccountId, 10m, cancellationToken)
                : StartTransfer(secondAccountId, firstAccountId, 10m, cancellationToken)));

        Assert.All(transferResults, transferResult => Assert.True(transferResult.IsSuccessful));
        var firstAccount = await AccountsScenario.GetAccountAsync(environment, firstAccountId, cancellationToken);
        var secondAccount = await AccountsScenario.GetAccountAsync(environment, secondAccountId, cancellationToken);
        Assert.Equal(20000m, firstAccount.Balance + secondAccount.Balance);
    }

    private Task<Guid> CreateActiveAccountAsync(string fullName, decimal balance, decimal overdraftLimit, CancellationToken cancellationToken)
    {
        return AccountsScenario.CreateAccountAsync(environment, fullName, AccountsScenario.UniqueDocumentNumber(), balance, overdraftLimit, AccountStatus.Active, cancellationToken);
    }

    private Task<Either<Error, Unit>> StartTransfer(Guid sourceAccountId, Guid destinationAccountId, decimal amount, CancellationToken cancellationToken)
    {
        return Task.Run(
            () => AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, amount, cancellationToken),
            cancellationToken);
    }
}
