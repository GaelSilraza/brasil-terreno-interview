using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Modules.Transfers.Domain.Services;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Application.Services;

public sealed class TransferExecutor(
    IAccountsModule accountsModule,
    ITransferRepository transferRepository,
    TransferLimitPolicyResolver transferLimitPolicyResolver,
    TransferPeriodSchedule transferPeriodSchedule,
    IAdvisoryLock advisoryLock,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan HourlyWindow = TimeSpan.FromHours(1);

    public async Task<Either<Error, Unit>> ExecuteAsync(Transfer transfer, CancellationToken cancellationToken)
    {
        await advisoryLock.AcquireTransactionLockAsync(TransferLockKeys.ForSourceAccount(transfer.SourceAccountId), cancellationToken);

        var now = timeProvider.GetUtcNow();
        var windowUsage = await transferRepository.GetWindowUsageAsync(transfer.SourceAccountId, now - HourlyWindow, cancellationToken);
        var resolvedPolicy = await transferLimitPolicyResolver.ResolveAsync(transfer.SourceAccountId, cancellationToken);

        transfer.RegisterAttempt(now);

        var policyEvaluation = resolvedPolicy.Policy.Evaluate(windowUsage, transfer.Amount, transferPeriodSchedule.Resolve(now));

        if (policyEvaluation.IsFailure)
        {
            return FailTransfer(transfer, policyEvaluation.Failure, now);
        }

        var fundsTransferRequest = new TransferFundsRequest(transfer.Id, transfer.SourceAccountId, transfer.DestinationAccountId, transfer.Amount);
        var fundsTransfer = await accountsModule.TransferFundsAsync(fundsTransferRequest, cancellationToken);

        if (fundsTransfer.IsFailure)
        {
            return FailTransfer(transfer, fundsTransfer.Failure, now);
        }

        return transfer.Complete(now);
    }

    private static Either<Error, Unit> FailTransfer(Transfer transfer, Error reason, DateTimeOffset now)
    {
        var failure = transfer.Fail(reason, now);
        return failure.IsFailure ? failure.Failure : reason;
    }
}
