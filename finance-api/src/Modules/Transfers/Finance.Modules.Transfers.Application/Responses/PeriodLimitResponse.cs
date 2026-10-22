namespace Finance.Modules.Transfers.Application.Responses;

public sealed record PeriodLimitResponse(decimal MaxAmountPerHour, int MaxAttemptsPerHour);
