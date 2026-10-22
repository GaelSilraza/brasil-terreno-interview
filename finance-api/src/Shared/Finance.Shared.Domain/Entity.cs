namespace Finance.Shared.Domain;

public abstract class Entity
{
    public Guid Id { get; protected init; }

    public DateTimeOffset CreatedAt { get; protected init; }
}
