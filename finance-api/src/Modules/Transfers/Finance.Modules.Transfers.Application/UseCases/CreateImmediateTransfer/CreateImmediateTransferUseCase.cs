using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Transfers.Application.UseCases.CreateImmediateTransfer;

public sealed class CreateImmediateTransferUseCase(
    IValidator<CreateImmediateTransferCommand> validator,
    IUnitOfWork unitOfWork,
    IAdvisoryLock advisoryLock,
    ITransferRepository transferRepository,
    IAccountsModule accountsModule,
    TransferExecutor transferExecutor,
    IdempotentTransferReplay idempotentTransferReplay,
    TransferResponseFactory transferResponseFactory,
    TimeProvider timeProvider)
{
    public async Task<Either<Error, TransferResult>> ExecuteAsync(
        CreateImmediateTransferCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var requestFingerprint = TransferRequestFingerprint.Compute(
            TransferType.Immediate,
            command.SourceAccountId,
            command.DestinationAccountId,
            command.Amount,
            scheduledFor: null);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (command.IdempotencyKey is not null)
        {
            await advisoryLock.AcquireTransactionLockAsync(TransferLockKeys.ForIdempotencyKey(command.IdempotencyKey), cancellationToken);
            var existingTransfer = await transferRepository.FindByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);

            if (existingTransfer is not null)
            {
                return await idempotentTransferReplay.ReplayAsync(existingTransfer, requestFingerprint, cancellationToken);
            }
        }

        var transferResult = Transfer.CreateImmediate(
            command.SourceAccountId,
            command.DestinationAccountId,
            command.Amount,
            command.IdempotencyKey,
            requestFingerprint,
            timeProvider.GetUtcNow());

        if (transferResult.IsFailure)
        {
            return transferResult.Failure;
        }

        var participantsResult = await accountsModule.FindTransferParticipantsAsync(command.SourceAccountId, command.DestinationAccountId, cancellationToken);

        if (participantsResult.IsFailure)
        {
            return participantsResult.Failure;
        }

        var transfer = transferResult.Success;
        transferRepository.Add(transfer);

        var execution = await transferExecutor.ExecuteAsync(transfer, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (execution.IsFailure)
        {
            return execution.Failure.WithMetadata(TransferMetadataKeys.TransferId, transfer.Id);
        }

        var response = await transferResponseFactory.CreateAsync(TransferReadModel.FromTransfer(transfer), cancellationToken);
        return new TransferResult(response, IsIdempotentReplay: false);
    }
}
