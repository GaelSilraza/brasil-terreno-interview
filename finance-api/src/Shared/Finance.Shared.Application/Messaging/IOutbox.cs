namespace Finance.Shared.Application.Messaging;

public interface IOutbox
{
    void Add<TMessage>(string routingKey, TMessage message, DateTimeOffset occurredAt)
        where TMessage : notnull;
}
