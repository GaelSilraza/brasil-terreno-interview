using Finance.Modules.Transfers.Domain.Enums;

namespace Finance.Modules.Transfers.Application.Messages;

public sealed record TransferEventMessage(
    Guid TransferId,
    string EventName,
    TransferStatus Status,
    string? FailureCode,
    DateTimeOffset OccurredAt);
