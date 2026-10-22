using Finance.Modules.Accounts.Application.Queries;
using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Infrastructure.Persistence.Views;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Modules.Accounts.Infrastructure.Persistence.Queries;

public sealed class AccountQueries(FinanceDbContext dbContext) : IAccountQueries
{
    public const string SearchQueryTag = "account-search";

    private const int MinimumTrigramSearchLength = 3;

    public Task<AccountResponse?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return ProjectAccounts(dbContext.Set<Account>().AsNoTracking().Where(account => account.Id == accountId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CursorPage<AccountResponse>> SearchAsync(
        AccountSearchTerm searchTerm,
        CursorPageRequest<AccountCursorKey> pageRequest,
        CancellationToken cancellationToken)
    {
        var persons = FilterPersons(searchTerm);

        if (pageRequest.After is { } after)
        {
            persons = pageRequest.Direction == CursorDirection.Next
                ? persons.Where(person => EF.Functions.GreaterThan(ValueTuple.Create(person.FullName, person.Id), ValueTuple.Create(after.FullName, after.PersonId)))
                : persons.Where(person => EF.Functions.LessThan(ValueTuple.Create(person.FullName, person.Id), ValueTuple.Create(after.FullName, after.PersonId)));
        }

        var pagePersons = pageRequest.Direction == CursorDirection.Next
            ? persons.OrderBy(person => person.FullName).ThenBy(person => person.Id).Take(pageRequest.Limit + 1)
            : persons.OrderByDescending(person => person.FullName).ThenByDescending(person => person.Id).Take(pageRequest.Limit + 1);

        var holders =
            from person in pagePersons
            join account in dbContext.Set<Account>().AsNoTracking() on person.Id equals account.PersonId
            join accountBalance in dbContext.Set<AccountBalance>() on account.Id equals accountBalance.AccountId
            select new { Person = person, Account = account, accountBalance.Balance };

        holders = pageRequest.Direction == CursorDirection.Next
            ? holders.OrderBy(holder => holder.Person.FullName).ThenBy(holder => holder.Person.Id)
            : holders.OrderByDescending(holder => holder.Person.FullName).ThenByDescending(holder => holder.Person.Id);

        var fetchedAccounts = await holders
            .TagWith(SearchQueryTag)
            .Select(holder => new AccountResponse(
                holder.Account.Id,
                holder.Account.PersonId,
                holder.Person.FullName,
                holder.Person.DocumentNumber,
                holder.Balance,
                holder.Account.OverdraftLimit,
                holder.Balance + holder.Account.OverdraftLimit,
                holder.Balance < 0 ? -holder.Balance : 0m,
                holder.Account.Status,
                holder.Account.CreatedAt,
                holder.Account.UpdatedAt))
            .ToListAsync(cancellationToken);

        return CursorPageBuilder.Build(fetchedAccounts, pageRequest, account => new AccountCursorKey(account.HolderName, account.PersonId));
    }

    public async Task<CursorPage<AccountEntryResponse>> ListEntriesAsync(
        Guid accountId,
        CursorPageRequest<EntryCursorKey> pageRequest,
        CancellationToken cancellationToken)
    {
        var accountEntries = dbContext.Set<AccountEntry>().AsNoTracking().Where(entry => entry.AccountId == accountId);

        if (pageRequest.After is { } after)
        {
            accountEntries = pageRequest.Direction == CursorDirection.Next
                ? accountEntries.Where(entry => entry.Id < after.Id)
                : accountEntries.Where(entry => entry.Id > after.Id);
        }

        var statement =
            from entry in accountEntries
            join counterpartAccount in dbContext.Set<Account>().AsNoTracking() on entry.CounterpartAccountId equals counterpartAccount.Id into counterpartAccounts
            from counterpartAccount in counterpartAccounts.DefaultIfEmpty()
            join counterpartPerson in dbContext.Set<Person>().AsNoTracking() on counterpartAccount!.PersonId equals counterpartPerson.Id into counterpartPersons
            from counterpartPerson in counterpartPersons.DefaultIfEmpty()
            select new { Entry = entry, CounterpartHolderName = counterpartPerson != null ? counterpartPerson.FullName : null };

        statement = pageRequest.Direction == CursorDirection.Next
            ? statement.OrderByDescending(statementLine => statementLine.Entry.Id)
            : statement.OrderBy(statementLine => statementLine.Entry.Id);

        var fetchedEntries = await statement
            .Take(pageRequest.Limit + 1)
            .Select(statementLine => new AccountEntryResponse(
                statementLine.Entry.Id,
                statementLine.Entry.AccountId,
                statementLine.Entry.TransferId,
                statementLine.Entry.CounterpartAccountId,
                statementLine.CounterpartHolderName,
                statementLine.Entry.EntryType,
                statementLine.Entry.Amount,
                statementLine.Entry.BalanceAfter,
                statementLine.Entry.CreatedAt))
            .ToListAsync(cancellationToken);

        return CursorPageBuilder.Build(fetchedEntries, pageRequest, entry => new EntryCursorKey(entry.Id));
    }

    public async Task<IReadOnlyList<AccountParticipant>> FindParticipantsAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        var distinctAccountIds = accountIds.Distinct().ToList();
        return await (
                from account in dbContext.Set<Account>().AsNoTracking()
                join person in dbContext.Set<Person>().AsNoTracking() on account.PersonId equals person.Id
                where distinctAccountIds.Contains(account.Id)
                select new AccountParticipant(account.Id, person.FullName, account.Status == AccountStatus.Active))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Person> FilterPersons(AccountSearchTerm searchTerm)
    {
        var persons = dbContext.Set<Person>().AsNoTracking();

        if (searchTerm.Kind == AccountSearchKind.DocumentNumberPrefix)
        {
            return persons.Where(person => EF.Functions.Like(person.DocumentNumber, searchTerm.Value + "%"));
        }

        if (searchTerm.Kind != AccountSearchKind.HolderName)
        {
            return persons;
        }

        var namePattern = "%" + EscapeLikePattern(searchTerm.Value) + "%";

        if (searchTerm.Value.Length < MinimumTrigramSearchLength)
        {
            return persons.Where(person => EF.Functions.ILike(person.FullName, namePattern, "\\"));
        }

        return dbContext.Set<Person>()
            .FromSql($"SELECT * FROM persons WHERE full_name ILIKE {namePattern} ESCAPE '\\' OFFSET 0")
            .AsNoTracking();
    }

    private static string EscapeLikePattern(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private IQueryable<AccountResponse> ProjectAccounts(IQueryable<Account> accounts)
    {
        return
            from account in accounts
            join person in dbContext.Set<Person>().AsNoTracking() on account.PersonId equals person.Id
            join accountBalance in dbContext.Set<AccountBalance>() on account.Id equals accountBalance.AccountId
            select new AccountResponse(
                account.Id,
                account.PersonId,
                person.FullName,
                person.DocumentNumber,
                accountBalance.Balance,
                account.OverdraftLimit,
                accountBalance.Balance + account.OverdraftLimit,
                accountBalance.Balance < 0 ? -accountBalance.Balance : 0m,
                account.Status,
                account.CreatedAt,
                account.UpdatedAt);
    }
}
