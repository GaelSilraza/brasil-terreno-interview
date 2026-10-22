namespace Finance.Modules.Transfers.Presentation.Requests;

public sealed record ScheduleTransferRequest(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, DateTimeOffset ScheduledFor);
