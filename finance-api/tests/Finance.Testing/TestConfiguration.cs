namespace Finance.Testing;

public static class TestConfiguration
{
    public static Dictionary<string, string?> CreateDefault(string connectionString)
    {
        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:Finance"] = connectionString,
            ["TransferLimits:TimeZoneId"] = "America/Sao_Paulo",
            ["TransferLimits:DaytimeStartsAt"] = "06:00",
            ["TransferLimits:NighttimeStartsAt"] = "20:00",
            ["TransferLimits:DefaultDaytimeMaxAmountPerHour"] = "5000",
            ["TransferLimits:DefaultDaytimeMaxAttemptsPerHour"] = "5",
            ["TransferLimits:DefaultNighttimeMaxAmountPerHour"] = "1000",
            ["TransferLimits:DefaultNighttimeMaxAttemptsPerHour"] = "3",
            ["ScheduledTransfers:PollingIntervalSeconds"] = "1",
            ["ScheduledTransfers:BatchSize"] = "50",
            ["ScheduledTransfers:StaleProcessingTimeoutMinutes"] = "10",
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Outbox:BatchSize"] = "100",
            ["Outbox:MaxAttempts"] = "10",
            ["RabbitMq:UserName"] = "finance",
            ["RabbitMq:Password"] = "finance",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:ConnectionAttempts"] = "3",
            ["Seed:Enabled"] = "false",
        };
    }
}
