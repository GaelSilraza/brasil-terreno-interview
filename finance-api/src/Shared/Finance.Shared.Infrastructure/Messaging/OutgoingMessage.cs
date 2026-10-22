namespace Finance.Shared.Infrastructure.Messaging;

public sealed record OutgoingMessage(Guid MessageId, string Type, string RoutingKey, string Content);
