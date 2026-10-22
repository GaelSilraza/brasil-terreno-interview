using Finance.Modules.Transfers.Application.Queries;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Transfers.Application.UseCases.ListAccountTransfers;

public sealed class ListAccountTransfersValidator : AbstractValidator<ListAccountTransfersQuery>
{
    public ListAccountTransfersValidator()
    {
        RuleFor(query => query.AccountId)
            .NotNull()
            .NotEqual(Guid.Empty)
                .WithErrorCode(FieldErrorCodes.Required)
                .WithMessage("'Account Id' must not be empty.");
        RuleFor(query => query.Limit).ValidLimit().When(query => query.Limit.HasValue);
        RuleFor(query => query.Direction).ValidDirection();
        RuleFor(query => query.Cursor).ValidCursor<ListAccountTransfersQuery, TransferCursorKey>();
    }
}
