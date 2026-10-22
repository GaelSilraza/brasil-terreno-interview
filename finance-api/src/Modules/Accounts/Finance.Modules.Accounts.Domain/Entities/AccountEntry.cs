using Finance.Modules.Accounts.Domain.Enums;
using Finance.Shared.Domain;

namespace Finance.Modules.Accounts.Domain.Entities;

public sealed class AccountEntry : Entity
{
    private AccountEntry()
    {
    }

    public Guid AccountId { get; private set; }

    public Guid? TransferId { get; private set; }

    public Guid? CounterpartAccountId { get; private set; }

    public AccountEntryType EntryType { get; private set; }

    public decimal Amount { get; private set; }

    public decimal BalanceAfter { get; private set; }

    public static AccountEntry Create(
        Guid accountId,
        Guid transferId,
        Guid counterpartAccountId,
        AccountEntryType entryType,
        decimal amount,
        decimal balanceAfter,
        DateTimeOffset now)
    {
        return new AccountEntry
        {
            Id = Guid.CreateVersion7(now),
            AccountId = accountId,
            TransferId = transferId,
            CounterpartAccountId = counterpartAccountId,
            EntryType = entryType,
            Amount = amount,
            BalanceAfter = balanceAfter,
            CreatedAt = now,
        };
    }

    public static AccountEntry CreateOpening(Guid accountId, decimal openingBalance, DateTimeOffset now)
    {
        return new AccountEntry
        {
            Id = Guid.CreateVersion7(now),
            AccountId = accountId,
            EntryType = openingBalance < 0 ? AccountEntryType.Debit : AccountEntryType.Credit,
            Amount = Math.Abs(openingBalance),
            BalanceAfter = openingBalance,
            CreatedAt = now,
        };
    }
}
