using Finance.Shared.Application.Messaging;

namespace Finance.Shared.Infrastructure.Tests.SampleModule;

public sealed class SampleMessageProcessor(ReceivedSampleMessages receivedSampleMessages) : IMessageProcessor<SampleMessage>
{
    public const string PoisonName = "poison";

    public Task ProcessAsync(SampleMessage message, CancellationToken cancellationToken)
    {
        if (message.Name == PoisonName)
        {
            throw new InvalidOperationException("Poison message.");
        }

        receivedSampleMessages.Add(message);
        return Task.CompletedTask;
    }
}
