namespace Finance.Modules.Transfers.Presentation.Requests;

public sealed record SetTransferLimitsRequest(
    decimal DaytimeMaxAmountPerHour,
    int DaytimeMaxAttemptsPerHour,
    decimal NighttimeMaxAmountPerHour,
    int NighttimeMaxAttemptsPerHour);
