using System.Globalization;
using Finance.Modules.Accounts.Application.Queries;
using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Application.UseCases.GetAccount;
using Finance.Modules.Accounts.Application.UseCases.ListAccountEntries;
using Finance.Modules.Accounts.Application.UseCases.ListAccounts;
using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Domain;
using Finance.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Tests.Support;

public static class AccountsScenario
{
    public static string UniqueDocumentNumber()
    {
        return Random.Shared.NextInt64(10000000000, 99999999999).ToString(CultureInfo.InvariantCulture);
    }

    public static async Task<Guid> CreateAccountAsync(
        FinanceTestEnvironment environment,
        string fullName,
        string documentNumber,
        decimal balance,
        decimal overdraftLimit,
        AccountStatus status,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var personRepository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = environment.TimeProvider.GetUtcNow();
        var person = Person.Create(fullName, documentNumber, now);
        var accountResult = Account.Open(person.Id, balance, overdraftLimit, status, now);

        if (accountResult.IsFailure)
        {
            throw new InvalidOperationException($"The scenario account is invalid: {accountResult.Failure.Code}.");
        }

        personRepository.Add(person);
        accountRepository.Add(accountResult.Success);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return accountResult.Success.Id;
    }

    public static async Task<Either<Error, Unit>> TransferFundsAsync(
        FinanceTestEnvironment environment,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var accountsModule = scope.ServiceProvider.GetRequiredService<IAccountsModule>();
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var request = new TransferFundsRequest(Guid.CreateVersion7(), sourceAccountId, destinationAccountId, amount);

        var transferResult = await accountsModule.TransferFundsAsync(request, cancellationToken);

        if (transferResult.IsSuccessful)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return transferResult;
    }

    public static async Task<AccountResponse> GetAccountAsync(FinanceTestEnvironment environment, Guid accountId, CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var getAccountUseCase = scope.ServiceProvider.GetRequiredService<GetAccountUseCase>();
        var accountResult = await getAccountUseCase.ExecuteAsync(accountId, cancellationToken);
        return accountResult.Success;
    }

    public static async Task<IReadOnlyList<AccountEntryResponse>> ListEntriesAsync(
        FinanceTestEnvironment environment,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var listAccountEntriesUseCase = scope.ServiceProvider.GetRequiredService<ListAccountEntriesUseCase>();
        var entriesResult = await listAccountEntriesUseCase.ExecuteAsync(
            new ListAccountEntriesQuery(accountId, null, null, CursorPageRequest<EntryCursorKey>.MaximumLimit),
            cancellationToken);
        return entriesResult.Success.Items;
    }

    public static async Task<Either<Error, CursorPage<AccountEntryResponse>>> ListEntriesPageAsync(
        FinanceTestEnvironment environment,
        ListAccountEntriesQuery query,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var listAccountEntriesUseCase = scope.ServiceProvider.GetRequiredService<ListAccountEntriesUseCase>();
        return await listAccountEntriesUseCase.ExecuteAsync(query, cancellationToken);
    }

    public static async Task<Either<Error, CursorPage<AccountResponse>>> ListAccountsAsync(
        FinanceTestEnvironment environment,
        ListAccountsQuery query,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var listAccountsUseCase = scope.ServiceProvider.GetRequiredService<ListAccountsUseCase>();
        return await listAccountsUseCase.ExecuteAsync(query, cancellationToken);
    }

    public static async Task<IReadOnlyList<AccountResponse>> WalkEveryPageForwardAsync(
        FinanceTestEnvironment environment,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var accounts = new List<AccountResponse>();
        string? cursor = null;

        do
        {
            var pageResult = await ListAccountsAsync(environment, new ListAccountsQuery(search, cursor, "next", limit), cancellationToken);
            var page = pageResult.Success;
            Assert.Equal(cursor is null, page.PreviousCursor is null);
            accounts.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return accounts;
    }

    public static async Task CreateAccountsAsync(
        FinanceTestEnvironment environment,
        IReadOnlyList<string> fullNames,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var personRepository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = environment.TimeProvider.GetUtcNow();
        var documentSequence = Random.Shared.NextInt64(10000000000, 90000000000);

        foreach (var fullName in fullNames)
        {
            documentSequence++;
            var person = Person.Create(fullName, documentSequence.ToString(CultureInfo.InvariantCulture), now);
            var accountResult = Account.Open(person.Id, 100m, 0m, AccountStatus.Active, now);
            personRepository.Add(person);
            accountRepository.Add(accountResult.Success);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public static string RandomUppercaseLetters(int length)
    {
        return new string(Enumerable.Range(0, length).Select(_ => (char)('A' + Random.Shared.Next(26))).ToArray());
    }
}
