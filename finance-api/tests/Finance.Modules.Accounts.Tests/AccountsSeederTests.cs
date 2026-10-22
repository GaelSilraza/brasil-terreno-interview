using Finance.Modules.Accounts.Application.UseCases.ListAccounts;
using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Infrastructure.Persistence.Seed;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Tests;

public sealed class AccountsSeederTests(AccountsTestFixture fixture) : IAsyncLifetime
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
    public async Task RunningSeedTwice_CreatesTheFiveSeedAccountsOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await SeedAsync(cancellationToken);
        await SeedAsync(cancellationToken);

        await using var scope = environment.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var personCount = await dbContext.Set<Person>().CountAsync(cancellationToken);
        Assert.Equal(5, personCount);
        var listAccountsUseCase = scope.ServiceProvider.GetRequiredService<ListAccountsUseCase>();
        var accountsResult = await listAccountsUseCase.ExecuteAsync(new ListAccountsQuery(null, null, null, null), cancellationToken);
        var accounts = accountsResult.Success;
        Assert.Equal(5, accounts.Items.Count);
        Assert.Null(accounts.NextCursor);
        Assert.Equal(-800m, Assert.Single(accounts.Items, account => account.HolderName == "Bruno Lima").Balance);
        Assert.Equal(AccountStatus.Blocked, Assert.Single(accounts.Items, account => account.HolderName == "Daniel Rocha").Status);
        Assert.Equal(AccountStatus.Inactive, Assert.Single(accounts.Items, account => account.HolderName == "Elisa Martins").Status);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var accountsSeeder = scope.ServiceProvider.GetRequiredService<AccountsSeeder>();
        await accountsSeeder.SeedAsync(cancellationToken);
    }
}
