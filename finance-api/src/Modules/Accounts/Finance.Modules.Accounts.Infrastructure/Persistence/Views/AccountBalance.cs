namespace Finance.Modules.Accounts.Infrastructure.Persistence.Views;

public sealed class AccountBalance
{
    public const string ViewName = "account_balances";

    public Guid AccountId { get; private set; }

    public decimal Balance { get; private set; }
}
