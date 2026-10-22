using Finance.Modules.Accounts.Domain.Entities;

namespace Finance.Modules.Accounts.Domain.Repositories;

public sealed record LockedAccount(Account Account, decimal Balance);
