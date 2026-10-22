namespace Finance.Modules.Transfers.Application.Options;

public sealed class ScheduledTransfersOptions
{
    public const string SectionName = "ScheduledTransfers";

    public int PollingIntervalSeconds { get; init; } = 5;

    public int BatchSize { get; init; } = 50;

    public int StaleProcessingTimeoutMinutes { get; init; } = 10;
}
