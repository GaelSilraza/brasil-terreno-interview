namespace Finance.Modules.Accounts.Presentation.Requests;

public sealed record OpenAccountRequest(string FullName, string DocumentNumber, decimal InitialBalance, decimal OverdraftLimit);
