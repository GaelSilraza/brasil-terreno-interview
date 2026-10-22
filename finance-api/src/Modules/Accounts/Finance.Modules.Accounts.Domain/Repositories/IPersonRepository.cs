using Finance.Modules.Accounts.Domain.Entities;

namespace Finance.Modules.Accounts.Domain.Repositories;

public interface IPersonRepository
{
    void Add(Person person);

    Task<bool> ExistsByDocumentNumberAsync(string documentNumber, CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);
}
