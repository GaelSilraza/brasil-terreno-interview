using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;

namespace Finance.Modules.Accounts.Application.Responses;

public sealed record AccountResponse(
    Guid Id,
    Guid PersonId,
    string HolderName,
    string DocumentNumber,
    decimal Balance,
    decimal OverdraftLimit,
    decimal AvailableAmount,
    decimal UsedOverdraft,
    AccountStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AccountResponse From(Account account, Person person, decimal balance)
    {
        return new AccountResponse(
            account.Id,
            account.PersonId,
            person.FullName,
            person.DocumentNumber,
            balance,
            account.OverdraftLimit,
            balance + account.OverdraftLimit,
            balance < 0 ? -balance : 0m,
            account.Status,
            account.CreatedAt,
            account.UpdatedAt);
    }
}
