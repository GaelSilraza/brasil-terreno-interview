using RabbitMQ.Client.Exceptions;

namespace Finance.Shared.Infrastructure.Messaging;

public static class RabbitMqFailures
{
    private const ushort AccessRefusedReplyCode = 403;

    private const ushort PreconditionFailedReplyCode = 406;

    private const ushort NotAllowedReplyCode = 530;

    public static bool IsConfigurationError(Exception exception)
    {
        return exception switch
        {
            AuthenticationFailureException or PossibleAuthenticationFailureException => true,
            OperationInterruptedException { ShutdownReason.ReplyCode: AccessRefusedReplyCode or PreconditionFailedReplyCode or NotAllowedReplyCode } => true,
            AggregateException aggregateException => aggregateException.InnerExceptions.Any(IsConfigurationError),
            { InnerException: { } innerException } => IsConfigurationError(innerException),
            _ => false,
        };
    }
}
