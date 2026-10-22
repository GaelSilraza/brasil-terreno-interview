namespace Finance.Shared.Infrastructure.Messaging;

public interface IMessagePublisher
{
    Task EnsureReadyAsync(CancellationToken cancellationToken);

    Task PublishAsync(OutgoingMessage outgoingMessage, CancellationToken cancellationToken);
}
