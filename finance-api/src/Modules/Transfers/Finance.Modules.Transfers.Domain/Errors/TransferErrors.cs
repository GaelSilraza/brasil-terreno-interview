using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Domain.Errors;

public static class TransferErrors
{
    public static readonly Error TransferNotFound = new("TransferNotFound", "The transfer was not found.", ErrorType.NotFound);

    public static readonly Error AccountNotFound = new("AccountNotFound", "The account was not found.", ErrorType.NotFound);

    public static readonly Error InvalidAmount = new("InvalidAmount", "The amount must be greater than zero.", ErrorType.Validation);

    public static readonly Error SameAccount = new("SameAccount", "Source and destination accounts must be different.", ErrorType.Validation);

    public static readonly Error ScheduleDateMustBeInFuture = new("ScheduleDateMustBeInFuture", "The scheduled date must be in the future.", ErrorType.Validation);

    public static readonly Error SourceAccountInactive = new("SourceAccountInactive", "The source account is blocked or inactive.", ErrorType.BusinessRule);

    public static readonly Error DestinationAccountInactive = new("DestinationAccountInactive", "The destination account is blocked or inactive.", ErrorType.BusinessRule);

    public static readonly Error AttemptLimitExceeded = new("AttemptLimitExceeded", "The limit of transfer attempts per hour was reached.", ErrorType.BusinessRule);

    public static readonly Error HourlyAmountLimitExceeded = new("HourlyAmountLimitExceeded", "The limit of transferred amount per hour was reached.", ErrorType.BusinessRule);

    public static readonly Error IdempotencyKeyReused = new("IdempotencyKeyReused", "The idempotency key was already used with a different request.", ErrorType.BusinessRule);

    public static readonly Error ProcessingTimeout = new("ProcessingTimeout", "The scheduled transfer stayed in processing for too long.", ErrorType.BusinessRule);

    public static readonly Error TransferCannotBeCancelled = new("TransferCannotBeCancelled", "Only scheduled transfers can be cancelled.", ErrorType.Conflict);

    public static readonly Error InvalidStatusTransition = new("InvalidStatusTransition", "The transfer cannot change to this status.", ErrorType.Conflict);

    public static Error FromFailureCode(string? failureCode)
    {
        var code = string.IsNullOrWhiteSpace(failureCode) ? "UnexpectedError" : failureCode;
        return new Error(code, $"The transfer failed with reason {code}.", ErrorType.BusinessRule);
    }
}
