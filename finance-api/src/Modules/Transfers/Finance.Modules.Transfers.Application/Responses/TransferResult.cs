namespace Finance.Modules.Transfers.Application.Responses;

public sealed record TransferResult(TransferResponse Transfer, bool IsIdempotentReplay);
