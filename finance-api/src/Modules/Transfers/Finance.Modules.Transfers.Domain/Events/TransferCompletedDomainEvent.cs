using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Domain.Events;

public sealed record TransferCompletedDomainEvent(Guid TransferId, DateTimeOffset OccurredAt) : IDomainEvent;
