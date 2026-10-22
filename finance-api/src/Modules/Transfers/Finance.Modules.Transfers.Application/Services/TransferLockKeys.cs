namespace Finance.Modules.Transfers.Application.Services;

public static class TransferLockKeys
{
    public static string ForIdempotencyKey(string idempotencyKey)
    {
        return $"transfers:idempotency:{idempotencyKey}";
    }

    public static string ForSourceAccount(Guid sourceAccountId)
    {
        return $"transfers:source-account:{sourceAccountId}";
    }
}
