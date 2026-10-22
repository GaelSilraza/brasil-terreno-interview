using Finance.Shared.Domain;

namespace Finance.Shared.Infrastructure.Tests.SampleModule;

public sealed class SampleAggregate : AggregateRoot
{
    private SampleAggregate()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public static SampleAggregate Create(string name, DateTimeOffset now)
    {
        var sampleAggregate = new SampleAggregate
        {
            Id = Guid.CreateVersion7(now),
            CreatedAt = now,
            UpdatedAt = now,
            Name = name,
        };

        sampleAggregate.RaiseDomainEvent(new SampleCreatedDomainEvent(sampleAggregate.Id, name, now));
        return sampleAggregate;
    }
}
