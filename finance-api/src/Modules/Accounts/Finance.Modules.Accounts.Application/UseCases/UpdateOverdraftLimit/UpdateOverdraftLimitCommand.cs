namespace Finance.Modules.Accounts.Application.UseCases.UpdateOverdraftLimit;

public sealed record UpdateOverdraftLimitCommand(Guid AccountId, decimal OverdraftLimit);
