using System.Globalization;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Services;

namespace Finance.Modules.Transfers.Tests;

public sealed class TransferPeriodScheduleTests
{
    [Theory]
    [InlineData("2026-10-20T09:00:00Z", TransferPeriod.Daytime)]
    [InlineData("2026-10-20T08:59:00Z", TransferPeriod.Nighttime)]
    [InlineData("2026-10-20T22:59:00Z", TransferPeriod.Daytime)]
    [InlineData("2026-10-20T23:00:00Z", TransferPeriod.Nighttime)]
    [InlineData("2026-10-21T03:00:00Z", TransferPeriod.Nighttime)]
    public void Moment_IsClassifiedUsingSaoPauloLocalTime(string utcMoment, TransferPeriod expectedPeriod)
    {
        var transferPeriodSchedule = new TransferPeriodSchedule("America/Sao_Paulo", new TimeOnly(6, 0), new TimeOnly(20, 0));
        var moment = DateTimeOffset.Parse(utcMoment, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        var period = transferPeriodSchedule.Resolve(moment);

        Assert.Equal(expectedPeriod, period);
    }
}
