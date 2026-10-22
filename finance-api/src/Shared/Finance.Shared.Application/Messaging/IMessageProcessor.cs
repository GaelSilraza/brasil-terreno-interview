namespace Finance.Shared.Application.Messaging;

public interface IMessageProcessor<in TMessage>
{
    Task ProcessAsync(TMessage message, CancellationToken cancellationToken);
}
