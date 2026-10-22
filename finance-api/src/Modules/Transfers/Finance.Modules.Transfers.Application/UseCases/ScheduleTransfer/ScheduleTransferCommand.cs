namespace Finance.Modules.Transfers.Application.UseCases.ScheduleTransfer;

public sealed record ScheduleTransferCommand(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, DateTimeOffset ScheduledFor, string? IdempotencyKey);
