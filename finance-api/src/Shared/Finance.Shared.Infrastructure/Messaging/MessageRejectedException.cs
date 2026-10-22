namespace Finance.Shared.Infrastructure.Messaging;

public sealed class MessageRejectedException(string message, Exception innerException) : Exception(message, innerException);
