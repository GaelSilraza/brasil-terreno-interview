using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Modules.Accounts.Infrastructure.Persistence.Repositories;

public sealed class PersonRepository(FinanceDbContext dbContext) : IPersonRepository
{
    public void Add(Person person)
    {
        dbContext.Set<Person>().Add(person);
    }

    public Task<bool> ExistsByDocumentNumberAsync(string documentNumber, CancellationToken cancellationToken)
    {
        return dbContext.Set<Person>().AsNoTracking().AnyAsync(person => person.DocumentNumber == documentNumber, cancellationToken);
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        return dbContext.Set<Person>().AsNoTracking().AnyAsync(cancellationToken);
    }
}
