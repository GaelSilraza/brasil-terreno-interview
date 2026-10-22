using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Modules.Transfers.Domain.Services;
using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Application.UseCases.GetTransferLimits;

public sealed class GetTransferLimitsUseCase(
    IAccountsModule accountsModule,
    TransferLimitPolicyResolver transferLimitPolicyResolver,
    ITransferRepository transferRepository,
    TransferPeriodSchedule transferPeriodSchedule,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan HourlyWindow = TimeSpan.FromHours(1);

    public async Task<Either<Error, TransferLimitsResponse>> ExecuteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var accountExists = await accountsModule.ExistsAsync(accountId, cancellationToken);

        if (!accountExists)
        {
            return TransferErrors.AccountNotFound;
        }

        var now = timeProvider.GetUtcNow();
        var resolvedPolicy = await transferLimitPolicyResolver.ResolveAsync(accountId, cancellationToken);
        var usage = await transferRepository.GetWindowUsageAsync(accountId, now - HourlyWindow, cancellationToken);
        var period = transferPeriodSchedule.Resolve(now);
        var currentLimit = resolvedPolicy.Policy.GetLimitFor(period);
        var daytimeLimit = resolvedPolicy.Policy.GetLimitFor(TransferPeriod.Daytime);
        var nighttimeLimit = resolvedPolicy.Policy.GetLimitFor(TransferPeriod.Nighttime);
        var remainingAttempts = Math.Max(0, currentLimit.MaxAttemptsPerHour - usage.AttemptCount);
        var remainingAmount = Math.Max(0m, currentLimit.MaxAmountPerHour - usage.CompletedAmount);

        return new TransferLimitsResponse(
            accountId,
            resolvedPolicy.IsCustom,
            period,
            new PeriodLimitResponse(daytimeLimit.MaxAmountPerHour, daytimeLimit.MaxAttemptsPerHour),
            new PeriodLimitResponse(nighttimeLimit.MaxAmountPerHour, nighttimeLimit.MaxAttemptsPerHour),
            new TransferLimitUsageResponse(usage.AttemptCount, usage.CompletedAmount, remainingAttempts, remainingAmount));
    }
}
