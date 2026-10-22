using System.Text.Json;
using Finance.Shared.Application.Messaging;
using Finance.Shared.Infrastructure.Json;
using Finance.Shared.Infrastructure.Persistence;

namespace Finance.Shared.Infrastructure.Outbox;

public sealed class EntityFrameworkOutbox(FinanceDbContext dbContext) : IOutbox
{
    public void Add<TMessage>(string routingKey, TMessage message, DateTimeOffset occurredAt)
        where TMessage : notnull
    {
        var content = JsonSerializer.Serialize(message, FinanceJsonSerializerOptions.Default);
        var outboxMessage = OutboxMessage.Create(typeof(TMessage).Name, routingKey, content, occurredAt);
        dbContext.Set<OutboxMessage>().Add(outboxMessage);
    }
}
