using System.Text.Json;

namespace Finance.EndToEnd.Tests.Support;

public sealed record AccountPayload(
    Guid Id,
    Guid PersonId,
    string HolderName,
    string DocumentNumber,
    decimal Balance,
    decimal OverdraftLimit,
    decimal AvailableAmount,
    decimal UsedOverdraft,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AccountEntryPayload(
    Guid Id,
    Guid AccountId,
    Guid? TransferId,
    Guid? CounterpartAccountId,
    string? CounterpartHolderName,
    string EntryType,
    decimal Amount,
    decimal BalanceAfter,
    DateTimeOffset CreatedAt);

public sealed record TransferPayload(
    Guid Id,
    Guid SourceAccountId,
    string SourceHolderName,
    Guid DestinationAccountId,
    string DestinationHolderName,
    decimal Amount,
    string Type,
    string Status,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset? AttemptedAt,
    DateTimeOffset? ProcessingStartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    DateTimeOffset? CancelledAt,
    string? FailureCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PeriodLimitPayload(decimal MaxAmountPerHour, int MaxAttemptsPerHour);

public sealed record TransferLimitUsagePayload(int AttemptsInLastHour, decimal CompletedAmountInLastHour, int RemainingAttempts, decimal RemainingAmount);

public sealed record TransferLimitsPayload(
    Guid AccountId,
    bool IsCustom,
    string CurrentPeriod,
    PeriodLimitPayload Daytime,
    PeriodLimitPayload Nighttime,
    TransferLimitUsagePayload Usage);

public sealed record CursorPagePayload<TItem>(IReadOnlyList<TItem> Items, string? NextCursor, string? PreviousCursor);

public sealed record FieldErrorPayload(string Field, string Code, string Message, Dictionary<string, JsonElement> Params);

public sealed record ProblemPayload(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Code,
    Guid? TransferId,
    Dictionary<string, string[]>? Errors,
    IReadOnlyList<FieldErrorPayload>? FieldErrors);
