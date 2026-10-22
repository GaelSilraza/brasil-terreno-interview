namespace Finance.Testing;

public sealed class ControllableTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private DateTimeOffset anchorUtcNow;
    private long anchorTimestamp;

    public ControllableTimeProvider(DateTimeOffset initialUtcNow)
    {
        SetUtcNow(initialUtcNow);
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        lock (gate)
        {
            anchorUtcNow = utcNow.ToUniversalTime();
            anchorTimestamp = System.GetTimestamp();
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return anchorUtcNow + System.GetElapsedTime(anchorTimestamp);
        }
    }
}
