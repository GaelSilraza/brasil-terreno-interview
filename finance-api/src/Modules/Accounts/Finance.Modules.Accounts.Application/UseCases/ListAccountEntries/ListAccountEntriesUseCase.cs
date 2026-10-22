using Finance.Modules.Accounts.Application.Queries;
using Finance.Modules.Accounts.Application.Responses;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.ListAccountEntries;

public sealed class ListAccountEntriesUseCase(
    IValidator<ListAccountEntriesQuery> validator,
    IAccountQueries accountQueries)
{
    public async Task<Either<Error, CursorPage<AccountEntryResponse>>> ExecuteAsync(ListAccountEntriesQuery query, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(query, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var pageRequest = CursorPageQueryRules.ToRequest<EntryCursorKey>(query.Cursor, query.Direction, query.Limit);
        return await accountQueries.ListEntriesAsync(query.AccountId, pageRequest, cancellationToken);
    }
}
