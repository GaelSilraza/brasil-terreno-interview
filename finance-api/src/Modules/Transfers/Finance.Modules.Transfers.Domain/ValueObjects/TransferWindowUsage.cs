namespace Finance.Modules.Transfers.Domain.ValueObjects;

public sealed record TransferWindowUsage(int AttemptCount, decimal CompletedAmount);
