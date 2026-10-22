namespace Finance.Modules.Transfers.Application.UseCases.CreateImmediateTransfer;

public sealed record CreateImmediateTransferCommand(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, string? IdempotencyKey);
