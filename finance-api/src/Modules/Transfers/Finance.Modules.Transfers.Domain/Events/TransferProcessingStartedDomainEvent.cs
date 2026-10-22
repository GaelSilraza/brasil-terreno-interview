using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Domain.Events;

public sealed record TransferProcessingStartedDomainEvent(Guid TransferId, DateTimeOffset OccurredAt) : IDomainEvent;
