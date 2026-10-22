namespace Finance.Modules.Transfers.Application.UseCases.ListAccountTransfers;

public sealed record ListAccountTransfersQuery(Guid? AccountId, string? Cursor, string? Direction, int? Limit);
