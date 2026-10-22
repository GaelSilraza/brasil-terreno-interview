using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Contracts;
using Finance.Shared.Application.Pagination;

namespace Finance.Modules.Accounts.Application.Queries;

public interface IAccountQueries
{
    Task<AccountResponse?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task<CursorPage<AccountResponse>> SearchAsync(
        AccountSearchTerm searchTerm,
        CursorPageRequest<AccountCursorKey> pageRequest,
        CancellationToken cancellationToken);

    Task<CursorPage<AccountEntryResponse>> ListEntriesAsync(
        Guid accountId,
        CursorPageRequest<EntryCursorKey> pageRequest,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountParticipant>> FindParticipantsAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);
}
