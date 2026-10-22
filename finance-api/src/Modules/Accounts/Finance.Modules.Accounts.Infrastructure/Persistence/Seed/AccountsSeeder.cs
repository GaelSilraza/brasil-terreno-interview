using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Shared.Application.Persistence;
using Microsoft.Extensions.Logging;

namespace Finance.Modules.Accounts.Infrastructure.Persistence.Seed;

public sealed class AccountsSeeder(
    IPersonRepository personRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AccountsSeeder> logger)
{
    private static readonly IReadOnlyList<SeedAccount> SeedAccounts =
    [
        new("Ana Souza", "10000000001", 500m, 1000m, AccountStatus.Active),
        new("Bruno Lima", "10000000002", -800m, 1000m, AccountStatus.Active),
        new("Carla Mendes", "10000000003", 10000m, 0m, AccountStatus.Active),
        new("Daniel Rocha", "10000000004", 300m, 0m, AccountStatus.Blocked),
        new("Elisa Martins", "10000000005", 2000m, 500m, AccountStatus.Inactive),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await personRepository.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Accounts seed skipped because persons already exist.");
            return;
        }

        var now = timeProvider.GetUtcNow();

        foreach (var seedAccount in SeedAccounts)
        {
            var person = Person.Create(seedAccount.FullName, seedAccount.DocumentNumber, now);
            var accountResult = Account.Open(person.Id, seedAccount.Balance, seedAccount.OverdraftLimit, seedAccount.Status, now);

            if (accountResult.IsFailure)
            {
                throw new InvalidOperationException($"Seed account {seedAccount.DocumentNumber} is invalid: {accountResult.Failure.Code}.");
            }

            personRepository.Add(person);
            accountRepository.Add(accountResult.Success);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Accounts seed created {SeedAccountCount} accounts.", SeedAccounts.Count);
    }

    private sealed record SeedAccount(string FullName, string DocumentNumber, decimal Balance, decimal OverdraftLimit, AccountStatus Status);
}
