using Finance.Modules.Accounts.Application.UseCases.UpdateAccountStatus;
using Finance.Modules.Accounts.Application.UseCases.UpdateOverdraftLimit;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Tests.Support;
using Finance.Shared.Domain;
using Finance.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Tests;

public sealed class AccountSettingsTests(AccountsTestFixture fixture) : IAsyncLifetime
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
    public async Task ChangingOverdraftLimit_OfExistingAccount_ChangesAvailableAmount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accountId = await AccountsScenario.CreateAccountAsync(environment, "Ana Souza", AccountsScenario.UniqueDocumentNumber(), 500m, 1000m, AccountStatus.Active, cancellationToken);

        var updateResult = await UpdateOverdraftLimitAsync(new UpdateOverdraftLimitCommand(accountId, 300m), cancellationToken);

        Assert.True(updateResult.IsSuccessful);
        var account = await AccountsScenario.GetAccountAsync(environment, accountId, cancellationToken);
        Assert.Equal(300m, account.OverdraftLimit);
        Assert.Equal(800m, account.AvailableAmount);
    }

    [Fact]
    public async Task ChangingOverdraftLimit_OfUnknownAccount_ReturnsAccountNotFoundWithoutException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var updateResult = await UpdateOverdraftLimitAsync(new UpdateOverdraftLimitCommand(Guid.CreateVersion7(), 300m), cancellationToken);

        Assert.True(updateResult.IsFailure);
        Assert.Equal("AccountNotFound", updateResult.Failure.Code);
    }

    [Fact]
    public async Task BlockingAccount_PreventsItFromSendingFunds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await AccountsScenario.CreateAccountAsync(environment, "Ana Souza", AccountsScenario.UniqueDocumentNumber(), 500m, 1000m, AccountStatus.Active, cancellationToken);
        var destinationAccountId = await AccountsScenario.CreateAccountAsync(environment, "Bruno Lima", AccountsScenario.UniqueDocumentNumber(), 100m, 0m, AccountStatus.Active, cancellationToken);

        var blockResult = await UpdateStatusAsync(new UpdateAccountStatusCommand(sourceAccountId, AccountStatus.Blocked), cancellationToken);
        var transferResult = await AccountsScenario.TransferFundsAsync(environment, sourceAccountId, destinationAccountId, 100m, cancellationToken);

        Assert.True(blockResult.IsSuccessful);
        var sourceAccount = await AccountsScenario.GetAccountAsync(environment, sourceAccountId, cancellationToken);
        Assert.Equal(AccountStatus.Blocked, sourceAccount.Status);
        Assert.True(transferResult.IsFailure);
        Assert.Equal("SourceAccountInactive", transferResult.Failure.Code);
        Assert.Equal(500m, sourceAccount.Balance);
    }

    [Fact]
    public async Task ChangingStatus_OfUnknownAccount_ReturnsAccountNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var updateResult = await UpdateStatusAsync(new UpdateAccountStatusCommand(Guid.CreateVersion7(), AccountStatus.Inactive), cancellationToken);

        Assert.True(updateResult.IsFailure);
        Assert.Equal("AccountNotFound", updateResult.Failure.Code);
    }

    private async Task<Either<Error, Unit>> UpdateOverdraftLimitAsync(UpdateOverdraftLimitCommand command, CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var updateOverdraftLimitUseCase = scope.ServiceProvider.GetRequiredService<UpdateOverdraftLimitUseCase>();
        return await updateOverdraftLimitUseCase.ExecuteAsync(command, cancellationToken);
    }

    private async Task<Either<Error, Unit>> UpdateStatusAsync(UpdateAccountStatusCommand command, CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var updateAccountStatusUseCase = scope.ServiceProvider.GetRequiredService<UpdateAccountStatusUseCase>();
        return await updateAccountStatusUseCase.ExecuteAsync(command, cancellationToken);
    }
}
