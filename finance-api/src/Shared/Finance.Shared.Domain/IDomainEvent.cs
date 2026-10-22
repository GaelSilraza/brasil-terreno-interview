namespace Finance.Shared.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
