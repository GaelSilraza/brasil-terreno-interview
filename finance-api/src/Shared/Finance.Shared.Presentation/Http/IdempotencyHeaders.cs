namespace Finance.Shared.Presentation.Http;

public static class IdempotencyHeaders
{
    public const string IdempotencyKey = "Idempotency-Key";

    public const string IdempotentReplayed = "Idempotent-Replayed";
}
