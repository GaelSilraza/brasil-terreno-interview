namespace Finance.Shared.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> domainEvents = [];

    public DateTimeOffset UpdatedAt { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        domainEvents.Add(domainEvent);
    }
}
