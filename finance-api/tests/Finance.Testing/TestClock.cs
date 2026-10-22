namespace Finance.Testing;

public static class TestClock
{
    public static readonly DateTimeOffset DaytimeInSaoPaulo = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset NighttimeInSaoPaulo = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);
}
