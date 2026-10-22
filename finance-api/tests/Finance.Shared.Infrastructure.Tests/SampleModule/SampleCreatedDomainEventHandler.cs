using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Messaging;

namespace Finance.Shared.Infrastructure.Tests.SampleModule;

public sealed class SampleCreatedDomainEventHandler(IOutbox outbox) : DomainEventHandler<SampleCreatedDomainEvent>
{
    protected override Task HandleDomainEventAsync(SampleCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add("testing.sample-created", new SampleMessage(domainEvent.SampleId, domainEvent.Name), domainEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
