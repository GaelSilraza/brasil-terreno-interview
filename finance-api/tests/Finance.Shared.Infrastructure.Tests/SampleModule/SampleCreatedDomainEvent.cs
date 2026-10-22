using Finance.Shared.Domain;

namespace Finance.Shared.Infrastructure.Tests.SampleModule;

public sealed record SampleCreatedDomainEvent(Guid SampleId, string Name, DateTimeOffset OccurredAt) : IDomainEvent;
