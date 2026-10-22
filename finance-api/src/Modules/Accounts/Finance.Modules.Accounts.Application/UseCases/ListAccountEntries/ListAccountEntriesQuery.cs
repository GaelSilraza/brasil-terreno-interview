namespace Finance.Modules.Accounts.Application.UseCases.ListAccountEntries;

public sealed record ListAccountEntriesQuery(Guid AccountId, string? Cursor, string? Direction, int? Limit);
