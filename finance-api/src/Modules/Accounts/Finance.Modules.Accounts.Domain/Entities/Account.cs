using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Domain.Errors;
using Finance.Shared.Domain;

namespace Finance.Modules.Accounts.Domain.Entities;

public sealed class Account : AggregateRoot
{
    public const decimal MaximumBalance = 9_999_999_999_999_999.99m;

    private readonly List<AccountEntry> entries = [];

    private Account()
    {
    }

    public Guid PersonId { get; private set; }

    public decimal OverdraftLimit { get; private set; }

    public AccountStatus Status { get; private set; }

    public IReadOnlyCollection<AccountEntry> Entries => entries.AsReadOnly();

    public bool IsActive => Status == AccountStatus.Active;

    public static Either<Error, Account> Open(
        Guid personId,
        decimal initialBalance,
        decimal overdraftLimit,
        AccountStatus status,
        DateTimeOffset now)
    {
        if (overdraftLimit < 0)
        {
            return AccountErrors.InvalidOverdraftLimit;
        }

        if (initialBalance + overdraftLimit < 0)
        {
            return AccountErrors.InvalidOpeningBalance;
        }

        var account = new Account
        {
            Id = Guid.CreateVersion7(now),
            PersonId = personId,
            OverdraftLimit = overdraftLimit,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (initialBalance != 0)
        {
            account.entries.Add(AccountEntry.CreateOpening(account.Id, initialBalance, now));
        }

        return account;
    }

    public Either<Error, Unit> TransferTo(
        Account destination,
        decimal sourceBalance,
        decimal destinationBalance,
        Guid transferId,
        decimal amount,
        DateTimeOffset now)
    {
        if (amount <= 0)
        {
            return AccountErrors.InvalidAmount;
        }

        if (destination.Id == Id)
        {
            return AccountErrors.SameAccount;
        }

        if (!IsActive)
        {
            return AccountErrors.SourceAccountInactive;
        }

        if (!destination.IsActive)
        {
            return AccountErrors.DestinationAccountInactive;
        }

        if (amount > sourceBalance + OverdraftLimit)
        {
            return AccountErrors.InsufficientFunds;
        }

        if (destinationBalance + amount > MaximumBalance)
        {
            return AccountErrors.DestinationBalanceLimitExceeded;
        }

        entries.Add(AccountEntry.Create(Id, transferId, destination.Id, AccountEntryType.Debit, amount, sourceBalance - amount, now));
        destination.entries.Add(AccountEntry.Create(destination.Id, transferId, Id, AccountEntryType.Credit, amount, destinationBalance + amount, now));
        return Unit.Value;
    }
}
