using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Domain.Events;

public sealed record TransferFailedDomainEvent(Guid TransferId, string FailureCode, DateTimeOffset OccurredAt) : IDomainEvent;
