using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Domain.Events;

public sealed record TransferScheduledDomainEvent(Guid TransferId, DateTimeOffset OccurredAt) : IDomainEvent;
