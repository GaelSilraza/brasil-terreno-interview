using Finance.Modules.Transfers.Domain.Enums;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Modules.Transfers.Domain.Events;
using Finance.Shared.Domain;

namespace Finance.Modules.Transfers.Domain.Entities;

public sealed class Transfer : AggregateRoot
{
    public const int IdempotencyKeyMaximumLength = 100;

    public const int FailureCodeMaximumLength = 50;

    public const int RequestFingerprintLength = 64;

    private Transfer()
    {
    }

    public Guid SourceAccountId { get; private set; }

    public Guid DestinationAccountId { get; private set; }

    public decimal Amount { get; private set; }

    public TransferType Type { get; private set; }

    public TransferStatus Status { get; private set; }

    public DateTimeOffset? ScheduledFor { get; private set; }

    public DateTimeOffset? AttemptedAt { get; private set; }

    public DateTimeOffset? ProcessingStartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? FailureCode { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public string? RequestFingerprint { get; private set; }

    public static Either<Error, Transfer> CreateImmediate(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string? idempotencyKey,
        string requestFingerprint,
        DateTimeOffset now)
    {
        var participantsError = ValidateParticipantsAndAmount(sourceAccountId, destinationAccountId, amount);

        if (participantsError is not null)
        {
            return participantsError;
        }

        return new Transfer
        {
            Id = Guid.CreateVersion7(now),
            SourceAccountId = sourceAccountId,
            DestinationAccountId = destinationAccountId,
            Amount = amount,
            Type = TransferType.Immediate,
            Status = TransferStatus.Processing,
            ProcessingStartedAt = now,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public static Either<Error, Transfer> Schedule(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        DateTimeOffset scheduledFor,
        string? idempotencyKey,
        string requestFingerprint,
        DateTimeOffset now)
    {
        var participantsError = ValidateParticipantsAndAmount(sourceAccountId, destinationAccountId, amount);

        if (participantsError is not null)
        {
            return participantsError;
        }

        if (scheduledFor <= now)
        {
            return TransferErrors.ScheduleDateMustBeInFuture;
        }

        var transfer = new Transfer
        {
            Id = Guid.CreateVersion7(now),
            SourceAccountId = sourceAccountId,
            DestinationAccountId = destinationAccountId,
            Amount = amount,
            Type = TransferType.Scheduled,
            Status = TransferStatus.Scheduled,
            ScheduledFor = scheduledFor.ToUniversalTime(),
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            CreatedAt = now,
            UpdatedAt = now,
        };
        transfer.RaiseDomainEvent(new TransferScheduledDomainEvent(transfer.Id, now));
        return transfer;
    }

    public bool MatchesFingerprint(string requestFingerprint)
    {
        return RequestFingerprint == requestFingerprint;
    }

    public void RegisterAttempt(DateTimeOffset now)
    {
        AttemptedAt = now;
        UpdatedAt = now;
    }

    public Either<Error, Unit> StartProcessing(DateTimeOffset now)
    {
        if (Status != TransferStatus.Scheduled)
        {
            return TransferErrors.InvalidStatusTransition;
        }

        Status = TransferStatus.Processing;
        ProcessingStartedAt = now;
        UpdatedAt = now;
        RaiseDomainEvent(new TransferProcessingStartedDomainEvent(Id, now));
        return Unit.Value;
    }

    public Either<Error, Unit> Complete(DateTimeOffset now)
    {
        if (Status != TransferStatus.Processing)
        {
            return TransferErrors.InvalidStatusTransition;
        }

        Status = TransferStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
        RaiseDomainEvent(new TransferCompletedDomainEvent(Id, now));
        return Unit.Value;
    }

    public Either<Error, Unit> Fail(Error reason, DateTimeOffset now)
    {
        if (Status != TransferStatus.Processing)
        {
            return TransferErrors.InvalidStatusTransition;
        }

        Status = TransferStatus.Failed;
        FailureCode = reason.Code;
        FailedAt = now;
        UpdatedAt = now;
        RaiseDomainEvent(new TransferFailedDomainEvent(Id, reason.Code, now));
        return Unit.Value;
    }

    public Either<Error, Unit> Cancel(DateTimeOffset now)
    {
        if (Status != TransferStatus.Scheduled)
        {
            return TransferErrors.TransferCannotBeCancelled;
        }

        Status = TransferStatus.Cancelled;
        CancelledAt = now;
        UpdatedAt = now;
        RaiseDomainEvent(new TransferCancelledDomainEvent(Id, now));
        return Unit.Value;
    }

    private static Error? ValidateParticipantsAndAmount(Guid sourceAccountId, Guid destinationAccountId, decimal amount)
    {
        if (amount <= 0)
        {
            return TransferErrors.InvalidAmount;
        }

        if (sourceAccountId == destinationAccountId)
        {
            return TransferErrors.SameAccount;
        }

        return null;
    }
}
