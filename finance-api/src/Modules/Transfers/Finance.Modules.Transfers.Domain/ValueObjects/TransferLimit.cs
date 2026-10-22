namespace Finance.Modules.Transfers.Domain.ValueObjects;

public sealed record TransferLimit(decimal MaxAmountPerHour, int MaxAttemptsPerHour);
