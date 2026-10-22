namespace Finance.Shared.Infrastructure.Messaging;

public sealed record RabbitMqConsumerQueue(string QueueName, string BindingKey);
