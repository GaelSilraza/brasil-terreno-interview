namespace Finance.Modules.Transfers.Application.Messages;

public static class TransferRoutingKeys
{
    public const string Scheduled = "transfers.scheduled";

    public const string ProcessingStarted = "transfers.processing-started";

    public const string Completed = "transfers.completed";

    public const string Failed = "transfers.failed";

    public const string Cancelled = "transfers.cancelled";

    public const string AllTransferEvents = "transfers.#";
}
