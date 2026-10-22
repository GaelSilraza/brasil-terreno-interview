using Finance.Modules.Transfers.Domain.Enums;

namespace Finance.Modules.Transfers.Application.Responses;

public sealed record TransferResponse(
    Guid Id,
    Guid SourceAccountId,
    string SourceHolderName,
    Guid DestinationAccountId,
    string DestinationHolderName,
    decimal Amount,
    TransferType Type,
    TransferStatus Status,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset? AttemptedAt,
    DateTimeOffset? ProcessingStartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    DateTimeOffset? CancelledAt,
    string? FailureCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
