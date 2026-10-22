using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Application.Responses;

namespace Finance.Modules.Transfers.Application.Services;

public sealed class TransferResponseFactory(IAccountsModule accountsModule)
{
    public async Task<TransferResponse> CreateAsync(TransferReadModel transfer, CancellationToken cancellationToken)
    {
        var responses = await CreateManyAsync([transfer], cancellationToken);
        return responses[0];
    }

    public async Task<IReadOnlyList<TransferResponse>> CreateManyAsync(
        IReadOnlyList<TransferReadModel> transfers,
        CancellationToken cancellationToken)
    {
        var accountIds = transfers
            .SelectMany(transfer => new[] { transfer.SourceAccountId, transfer.DestinationAccountId })
            .Distinct()
            .ToList();
        var holderNames = await accountsModule.FindHolderNamesAsync(accountIds, cancellationToken);

        return transfers
            .Select(transfer => new TransferResponse(
                transfer.Id,
                transfer.SourceAccountId,
                holderNames.GetValueOrDefault(transfer.SourceAccountId, string.Empty),
                transfer.DestinationAccountId,
                holderNames.GetValueOrDefault(transfer.DestinationAccountId, string.Empty),
                transfer.Amount,
                transfer.Type,
                transfer.Status,
                transfer.ScheduledFor,
                transfer.AttemptedAt,
                transfer.ProcessingStartedAt,
                transfer.CompletedAt,
                transfer.FailedAt,
                transfer.CancelledAt,
                transfer.FailureCode,
                transfer.CreatedAt,
                transfer.UpdatedAt))
            .ToList();
    }
}
