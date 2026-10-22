using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.Services;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Transfers.Application.UseCases.ListAccountTransfers;

public sealed class ListAccountTransfersUseCase(
    IValidator<ListAccountTransfersQuery> validator,
    ITransferQueries transferQueries,
    TransferResponseFactory transferResponseFactory)
{
    public async Task<Either<Error, CursorPage<TransferResponse>>> ExecuteAsync(ListAccountTransfersQuery query, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(query, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var accountId = query.AccountId.GetValueOrDefault();
        var pageRequest = CursorPageQueryRules.ToRequest<TransferCursorKey>(query.Cursor, query.Direction, query.Limit);
        var page = await transferQueries.ListByAccountAsync(accountId, pageRequest, cancellationToken);
        var responses = await transferResponseFactory.CreateManyAsync(page.Items, cancellationToken);
        return new CursorPage<TransferResponse>(responses, page.NextCursor, page.PreviousCursor);
    }
}
