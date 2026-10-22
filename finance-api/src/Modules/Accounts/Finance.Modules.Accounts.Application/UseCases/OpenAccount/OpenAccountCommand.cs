namespace Finance.Modules.Accounts.Application.UseCases.OpenAccount;

public sealed record OpenAccountCommand(string FullName, string DocumentNumber, decimal InitialBalance, decimal OverdraftLimit);
