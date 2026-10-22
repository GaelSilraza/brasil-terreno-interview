using Finance.Shared.Infrastructure.Messaging;

namespace Finance.Shared.Infrastructure.Tests.Support;

public sealed class InterruptingMessagePublisher(
    IMessagePublisher innerMessagePublisher,
    Func<int, Task> beforePublishingAsync) : IMessagePublisher
{
    private int publishCount;

    public Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        return innerMessagePublisher.EnsureReadyAsync(cancellationToken);
    }

    public async Task PublishAsync(OutgoingMessage outgoingMessage, CancellationToken cancellationToken)
    {
        publishCount++;
        await beforePublishingAsync(publishCount);
        await innerMessagePublisher.PublishAsync(outgoingMessage, cancellationToken);
    }
}
