namespace Finance.Modules.Transfers.Application.Options;

public sealed class TransferLimitsOptions
{
    public const string SectionName = "TransferLimits";

    public string TimeZoneId { get; init; } = "America/Sao_Paulo";

    public TimeOnly DaytimeStartsAt { get; init; } = new(6, 0);

    public TimeOnly NighttimeStartsAt { get; init; } = new(20, 0);

    public decimal DefaultDaytimeMaxAmountPerHour { get; init; } = 5000m;

    public int DefaultDaytimeMaxAttemptsPerHour { get; init; } = 5;

    public decimal DefaultNighttimeMaxAmountPerHour { get; init; } = 1000m;

    public int DefaultNighttimeMaxAttemptsPerHour { get; init; } = 3;
}
