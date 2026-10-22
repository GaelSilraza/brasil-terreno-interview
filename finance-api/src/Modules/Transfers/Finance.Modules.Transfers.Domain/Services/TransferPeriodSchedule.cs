using Finance.Modules.Transfers.Domain.Enums;

namespace Finance.Modules.Transfers.Domain.Services;

public sealed class TransferPeriodSchedule
{
    private readonly TimeZoneInfo timeZone;
    private readonly TimeOnly daytimeStartsAt;
    private readonly TimeOnly nighttimeStartsAt;

    public TransferPeriodSchedule(string timeZoneId, TimeOnly daytimeStartsAt, TimeOnly nighttimeStartsAt)
    {
        if (daytimeStartsAt >= nighttimeStartsAt)
        {
            throw new ArgumentException("The daytime must start before the nighttime.", nameof(daytimeStartsAt));
        }

        timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        this.daytimeStartsAt = daytimeStartsAt;
        this.nighttimeStartsAt = nighttimeStartsAt;
    }

    public TransferPeriod Resolve(DateTimeOffset moment)
    {
        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, timeZone).DateTime);
        return localTime >= daytimeStartsAt && localTime < nighttimeStartsAt
            ? TransferPeriod.Daytime
            : TransferPeriod.Nighttime;
    }
}
