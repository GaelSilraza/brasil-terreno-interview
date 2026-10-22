namespace Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;

public sealed record SetTransferLimitsCommand(
    Guid AccountId,
    decimal DaytimeMaxAmountPerHour,
    int DaytimeMaxAttemptsPerHour,
    decimal NighttimeMaxAmountPerHour,
    int NighttimeMaxAttemptsPerHour);
