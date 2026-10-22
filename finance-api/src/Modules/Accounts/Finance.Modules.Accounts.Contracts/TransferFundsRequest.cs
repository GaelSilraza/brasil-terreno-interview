namespace Finance.Modules.Accounts.Contracts;

public sealed record TransferFundsRequest(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount);
