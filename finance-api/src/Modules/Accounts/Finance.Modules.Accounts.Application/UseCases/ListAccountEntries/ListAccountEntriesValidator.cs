using Finance.Modules.Accounts.Application.Queries;
using Finance.Shared.Application.Pagination;
using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.ListAccountEntries;

public sealed class ListAccountEntriesValidator : AbstractValidator<ListAccountEntriesQuery>
{
    public ListAccountEntriesValidator()
    {
        RuleFor(query => query.Limit).ValidLimit().When(query => query.Limit.HasValue);
        RuleFor(query => query.Direction).ValidDirection();
        RuleFor(query => query.Cursor).ValidCursor<ListAccountEntriesQuery, EntryCursorKey>();
    }
}
