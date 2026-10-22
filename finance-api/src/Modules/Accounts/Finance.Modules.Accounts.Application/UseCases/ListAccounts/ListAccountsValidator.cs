using Finance.Modules.Accounts.Application.Queries;
using Finance.Shared.Application.Pagination;
using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.ListAccounts;

public sealed class ListAccountsValidator : AbstractValidator<ListAccountsQuery>
{
    public ListAccountsValidator()
    {
        RuleFor(query => query.Search).MaximumLength(AccountSearchTerm.MaximumLength);
        RuleFor(query => query.Limit).ValidLimit().When(query => query.Limit.HasValue);
        RuleFor(query => query.Direction).ValidDirection();
        RuleFor(query => query.Cursor).ValidCursor<ListAccountsQuery, AccountCursorKey>();
    }
}
