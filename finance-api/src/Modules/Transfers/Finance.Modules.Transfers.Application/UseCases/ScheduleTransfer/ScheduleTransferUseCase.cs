using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Transfers.Application.UseCases.ScheduleTransfer;

public sealed class ScheduleTransferUseCase(
    IValidator<ScheduleTransferCommand> validator,
    IUnitOfWork unitOfWork,
    IAdvisoryLock advisoryLock,
    ITransferRepository transferRepository,
    IAccountsModule accountsModule,
    IdempotentTransferReplay idempotentTransferReplay,
    TransferResponseFactory transferResponseFactory,
    TimeProvider timeProvider)
{
    public async Task<Either<Error, TransferResult>> ExecuteAsync(
        ScheduleTransferCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var requestFingerprint = TransferRequestFingerprint.Compute(
            TransferType.Scheduled,
            command.SourceAccountId,
            command.DestinationAccountId,
            command.Amount,
            command.ScheduledFor);

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

        var transferResult = Transfer.Schedule(
            command.SourceAccountId,
            command.DestinationAccountId,
            command.Amount,
            command.ScheduledFor.ToUniversalTime(),
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

        var participants = participantsResult.Success;

        if (!participants.Source.IsActive)
        {
            return TransferErrors.SourceAccountInactive;
        }

        if (!participants.Destination.IsActive)
        {
            return TransferErrors.DestinationAccountInactive;
        }

        var transfer = transferResult.Success;
        transferRepository.Add(transfer);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = await transferResponseFactory.CreateAsync(TransferReadModel.FromTransfer(transfer), cancellationToken);
        return new TransferResult(response, IsIdempotentReplay: false);
    }
}
