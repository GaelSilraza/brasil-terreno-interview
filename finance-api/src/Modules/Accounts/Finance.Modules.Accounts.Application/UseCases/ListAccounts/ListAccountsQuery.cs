namespace Finance.Modules.Accounts.Application.UseCases.ListAccounts;

public sealed record ListAccountsQuery(string? Search, string? Cursor, string? Direction, int? Limit);
