namespace Finance.Modules.Accounts.Contracts;

public sealed record AccountParticipant(Guid AccountId, string HolderName, bool IsActive);
