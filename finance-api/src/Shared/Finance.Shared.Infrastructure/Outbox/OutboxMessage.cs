namespace Finance.Shared.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public const int LastErrorMaximumLength = 2000;

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(string type, string routingKey, string content, DateTimeOffset occurredAt)
    {
        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(occurredAt),
            Type = type,
            RoutingKey = routingKey,
            Content = content,
            OccurredAt = occurredAt,
            AttemptCount = 0,
        };
    }

    public void MarkAsProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        LastError = null;
    }

    public void RegisterFailure(string errorMessage)
    {
        AttemptCount++;
        LastError = errorMessage.Length > LastErrorMaximumLength ? errorMessage[..LastErrorMaximumLength] : errorMessage;
    }
}
