using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Enums;

namespace Finance.Modules.Transfers.Application.Queries;

public sealed record TransferReadModel(
    Guid Id,
    Guid SourceAccountId,
    Guid DestinationAccountId,
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
    DateTimeOffset UpdatedAt)
{
    public static TransferReadModel FromTransfer(Transfer transfer)
    {
        return new TransferReadModel(
            transfer.Id,
            transfer.SourceAccountId,
            transfer.DestinationAccountId,
            transfer.Amount,
            transfer.Type,
            transfer.Status,
            transfer.ScheduledFor,
            transfer.AttemptedAt,
            transfer.ProcessingStartedAt,
            transfer.CompletedAt,
            transfer.FailedAt,
            transfer.CancelledAt,
            transfer.FailureCode,
            transfer.CreatedAt,
            transfer.UpdatedAt);
    }
}
