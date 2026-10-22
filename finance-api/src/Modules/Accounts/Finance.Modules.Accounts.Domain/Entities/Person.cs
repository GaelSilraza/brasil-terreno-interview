using Finance.Shared.Domain;

namespace Finance.Modules.Accounts.Domain.Entities;

public sealed class Person : AggregateRoot
{
    public const int FullNameMaximumLength = 150;

    public const int DocumentNumberLength = 11;

    private Person()
    {
    }

    public string FullName { get; private set; } = string.Empty;

    public string DocumentNumber { get; private set; } = string.Empty;

    public static Person Create(string fullName, string documentNumber, DateTimeOffset now)
    {
        return new Person
        {
            Id = Guid.CreateVersion7(now),
            FullName = fullName.Trim(),
            DocumentNumber = documentNumber,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
