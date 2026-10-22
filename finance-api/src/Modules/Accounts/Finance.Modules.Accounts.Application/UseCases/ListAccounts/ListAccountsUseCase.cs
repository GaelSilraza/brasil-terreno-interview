using Finance.Modules.Accounts.Application.Queries;
using Finance.Modules.Accounts.Application.Responses;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.ListAccounts;

public sealed class ListAccountsUseCase(
    IValidator<ListAccountsQuery> validator,
    IAccountQueries accountQueries)
{
    public async Task<Either<Error, CursorPage<AccountResponse>>> ExecuteAsync(ListAccountsQuery query, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(query, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var searchTerm = AccountSearchTerm.Parse(query.Search);
        var pageRequest = CursorPageQueryRules.ToRequest<AccountCursorKey>(query.Cursor, query.Direction, query.Limit);
        return await accountQueries.SearchAsync(searchTerm, pageRequest, cancellationToken);
    }
}
