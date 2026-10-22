using Finance.Modules.Accounts.Domain.Enums;

namespace Finance.Modules.Accounts.Application.Responses;

public sealed record AccountEntryResponse(
    Guid Id,
    Guid AccountId,
    Guid? TransferId,
    Guid? CounterpartAccountId,
    string? CounterpartHolderName,
    AccountEntryType EntryType,
    decimal Amount,
    decimal BalanceAfter,
    DateTimeOffset CreatedAt);
