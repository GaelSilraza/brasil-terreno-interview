namespace Finance.Modules.Transfers.Presentation.Requests;

public sealed record CreateTransferRequest(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount);
