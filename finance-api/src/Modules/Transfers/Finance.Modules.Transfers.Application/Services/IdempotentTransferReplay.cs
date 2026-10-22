using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Application.Services;

public sealed class IdempotentTransferReplay(TransferResponseFactory transferResponseFactory)
{
    public async Task<Either<Error, TransferResult>> ReplayAsync(
        Transfer existingTransfer,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        if (!existingTransfer.MatchesFingerprint(requestFingerprint))
        {
            return TransferErrors.IdempotencyKeyReused;
        }

        if (existingTransfer.Type == TransferType.Immediate && existingTransfer.Status == TransferStatus.Failed)
        {
            return TransferErrors.FromFailureCode(existingTransfer.FailureCode)
                .WithMetadata(TransferMetadataKeys.TransferId, existingTransfer.Id)
                .WithMetadata(ErrorMetadataKeys.IdempotentReplayed, true);
        }

        var response = await transferResponseFactory.CreateAsync(TransferReadModel.FromTransfer(existingTransfer), cancellationToken);
        return new TransferResult(response, IsIdempotentReplay: true);
    }
}
