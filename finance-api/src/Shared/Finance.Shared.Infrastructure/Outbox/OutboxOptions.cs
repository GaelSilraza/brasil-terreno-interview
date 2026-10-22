namespace Finance.Shared.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollingIntervalSeconds { get; init; } = 1;

    public int BatchSize { get; init; } = 500;

    public int MaxAttempts { get; init; } = 10;
}
