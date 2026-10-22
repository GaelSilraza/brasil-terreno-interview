using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Application.UseCases.OpenAccount;
using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Tests.Support;
using Finance.Shared.Domain;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Tests;

public sealed class OpeningAccountTests(AccountsTestFixture fixture) : IAsyncLifetime
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
    public async Task OpeningAccount_WithValidData_CreatesActiveAccountWithHolder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new OpenAccountCommand("Fernanda Alves", "10000000006", 250m, 300m);

        var accountResult = await OpenAccountAsync(command, cancellationToken);

        Assert.True(accountResult.IsSuccessful);
        var account = await AccountsScenario.GetAccountAsync(environment, accountResult.Success.Id, cancellationToken);
        Assert.Equal("Fernanda Alves", account.HolderName);
        Assert.Equal("10000000006", account.DocumentNumber);
        Assert.Equal(250m, account.Balance);
        Assert.Equal(300m, account.OverdraftLimit);
        Assert.Equal(550m, account.AvailableAmount);
        Assert.Equal(0m, account.UsedOverdraft);
        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public async Task OpeningAccount_WithInitialBalance_RecordsItAsTheOpeningEntryOfTheLedger()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new OpenAccountCommand("Fernanda Alves", "10000000006", 250m, 300m);

        var accountResult = await OpenAccountAsync(command, cancellationToken);

        var openingEntry = Assert.Single(await AccountsScenario.ListEntriesAsync(environment, accountResult.Success.Id, cancellationToken));
        Assert.Null(openingEntry.TransferId);
        Assert.Null(openingEntry.CounterpartAccountId);
        Assert.Null(openingEntry.CounterpartHolderName);
        Assert.Equal(AccountEntryType.Credit, openingEntry.EntryType);
        Assert.Equal(250m, openingEntry.Amount);
        Assert.Equal(250m, openingEntry.BalanceAfter);
    }

    [Fact]
    public async Task OpeningAccount_WithZeroBalance_HasAnEmptyLedgerAndZeroBalance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new OpenAccountCommand("Fernanda Alves", "10000000006", 0m, 300m);

        var accountResult = await OpenAccountAsync(command, cancellationToken);

        Assert.Empty(await AccountsScenario.ListEntriesAsync(environment, accountResult.Success.Id, cancellationToken));
        var account = await AccountsScenario.GetAccountAsync(environment, accountResult.Success.Id, cancellationToken);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(300m, account.AvailableAmount);
    }

    [Fact]
    public async Task OpeningAccountInOverdraft_RecordsADebitOpeningEntryAndDerivesTheNegativeBalance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var accountId = await AccountsScenario.CreateAccountAsync(
            environment,
            "Bruno Lima",
            AccountsScenario.UniqueDocumentNumber(),
            -800m,
            1000m,
            AccountStatus.Active,
            cancellationToken);

        var openingEntry = Assert.Single(await AccountsScenario.ListEntriesAsync(environment, accountId, cancellationToken));
        Assert.Equal(AccountEntryType.Debit, openingEntry.EntryType);
        Assert.Equal(800m, openingEntry.Amount);
        Assert.Equal(-800m, openingEntry.BalanceAfter);
        var account = await AccountsScenario.GetAccountAsync(environment, accountId, cancellationToken);
        Assert.Equal(-800m, account.Balance);
        Assert.Equal(800m, account.UsedOverdraft);
        Assert.Equal(200m, account.AvailableAmount);
    }

    [Fact]
    public async Task OpeningAccount_WithAlreadyRegisteredDocument_ReturnsDocumentNumberAlreadyExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstCommand = new OpenAccountCommand("Fernanda Alves", "10000000006", 250m, 300m);
        var secondCommand = new OpenAccountCommand("Gustavo Prado", "10000000006", 100m, 0m);
        await OpenAccountAsync(firstCommand, cancellationToken);

        var secondResult = await OpenAccountAsync(secondCommand, cancellationToken);

        Assert.True(secondResult.IsFailure);
        Assert.Equal("DocumentNumberAlreadyExists", secondResult.Failure.Code);
        await using var scope = environment.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var personCount = await dbContext.Set<Person>().CountAsync(cancellationToken);
        Assert.Equal(1, personCount);
    }

    [Fact]
    public async Task OpeningAccount_WithInvalidData_ReturnsValidationErrorsForEachField()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new OpenAccountCommand("A", "123", -1m, -1m);

        var accountResult = await OpenAccountAsync(command, cancellationToken);

        Assert.True(accountResult.IsFailure);
        Assert.Equal("ValidationFailed", accountResult.Failure.Code);
        Assert.Contains("fullName", accountResult.Failure.ValidationErrors.Keys);
        Assert.Contains("documentNumber", accountResult.Failure.ValidationErrors.Keys);
        Assert.Contains("initialBalance", accountResult.Failure.ValidationErrors.Keys);
        Assert.Contains("overdraftLimit", accountResult.Failure.ValidationErrors.Keys);
        Assert.Contains(accountResult.Failure.FieldErrors, fieldError => fieldError.Field == "fullName" && fieldError.Code == FieldErrorCodes.InvalidLength);
        Assert.Contains(accountResult.Failure.FieldErrors, fieldError => fieldError.Field == "documentNumber" && fieldError.Code == FieldErrorCodes.InvalidFormat);
        Assert.Contains(accountResult.Failure.FieldErrors, fieldError => fieldError.Field == "initialBalance" && fieldError.Code == FieldErrorCodes.OutOfRange);
        Assert.Contains(accountResult.Failure.FieldErrors, fieldError => fieldError.Field == "overdraftLimit" && fieldError.Code == FieldErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task OpeningAccount_WithTooManyDecimals_ReturnsInvalidPrecision()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new OpenAccountCommand("Fernanda Alves", "10000000006", 10.123m, 0m);

        var accountResult = await OpenAccountAsync(command, cancellationToken);

        Assert.True(accountResult.IsFailure);
        Assert.Equal("ValidationFailed", accountResult.Failure.Code);
        var fieldError = Assert.Single(accountResult.Failure.FieldErrors);
        Assert.Equal("initialBalance", fieldError.Field);
        Assert.Equal(FieldErrorCodes.InvalidPrecision, fieldError.Code);
        Assert.Equal(2, Assert.IsType<int>(fieldError.Params["expectedScale"]));
    }

    private async Task<Either<Error, AccountResponse>> OpenAccountAsync(
        OpenAccountCommand command,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var openAccountUseCase = scope.ServiceProvider.GetRequiredService<OpenAccountUseCase>();
        return await openAccountUseCase.ExecuteAsync(command, cancellationToken);
    }
}
