using System.Collections.Concurrent;

namespace Finance.Shared.Infrastructure.Tests.SampleModule;

public sealed class ReceivedSampleMessages
{
    private readonly ConcurrentQueue<SampleMessage> messages = new();

    public IReadOnlyCollection<SampleMessage> All => messages.ToArray();

    public void Add(SampleMessage message)
    {
        messages.Enqueue(message);
    }
}
