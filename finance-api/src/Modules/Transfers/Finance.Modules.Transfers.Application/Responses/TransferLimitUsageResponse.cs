namespace Finance.Modules.Transfers.Application.Responses;

public sealed record TransferLimitUsageResponse(int AttemptsInLastHour, decimal CompletedAmountInLastHour, int RemainingAttempts, decimal RemainingAmount);
