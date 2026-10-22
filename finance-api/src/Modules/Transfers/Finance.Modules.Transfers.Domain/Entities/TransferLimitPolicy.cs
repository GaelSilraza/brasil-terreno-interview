using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Modules.Transfers.Domain.ValueObjects;
using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Domain.Entities;

public sealed class TransferLimitPolicy
{
    private TransferLimitPolicy()
    {
    }

    public Guid AccountId { get; private set; }

    public decimal DaytimeMaxAmountPerHour { get; private set; }

    public int DaytimeMaxAttemptsPerHour { get; private set; }

    public decimal NighttimeMaxAmountPerHour { get; private set; }

    public int NighttimeMaxAttemptsPerHour { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TransferLimitPolicy Create(
        Guid accountId,
        decimal daytimeMaxAmountPerHour,
        int daytimeMaxAttemptsPerHour,
        decimal nighttimeMaxAmountPerHour,
        int nighttimeMaxAttemptsPerHour,
        DateTimeOffset now)
    {
        return new TransferLimitPolicy
        {
            AccountId = accountId,
            DaytimeMaxAmountPerHour = daytimeMaxAmountPerHour,
            DaytimeMaxAttemptsPerHour = daytimeMaxAttemptsPerHour,
            NighttimeMaxAmountPerHour = nighttimeMaxAmountPerHour,
            NighttimeMaxAttemptsPerHour = nighttimeMaxAttemptsPerHour,
            UpdatedAt = now,
        };
    }

    public TransferLimit GetLimitFor(TransferPeriod period)
    {
        return period == TransferPeriod.Daytime
            ? new TransferLimit(DaytimeMaxAmountPerHour, DaytimeMaxAttemptsPerHour)
            : new TransferLimit(NighttimeMaxAmountPerHour, NighttimeMaxAttemptsPerHour);
    }

    public Either<Error, Unit> Evaluate(TransferWindowUsage usage, decimal amount, TransferPeriod period)
    {
        var limit = GetLimitFor(period);

        if (usage.AttemptCount + 1 > limit.MaxAttemptsPerHour)
        {
            return TransferErrors.AttemptLimitExceeded;
        }

        if (usage.CompletedAmount + amount > limit.MaxAmountPerHour)
        {
            return TransferErrors.HourlyAmountLimitExceeded;
        }

        return Unit.Value;
    }
}
