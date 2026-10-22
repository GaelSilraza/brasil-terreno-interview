using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Domain.Events;

public sealed record TransferCancelledDomainEvent(Guid TransferId, DateTimeOffset OccurredAt) : IDomainEvent;
