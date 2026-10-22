using Finance.Modules.Transfers.Domain.Entities;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Transfers.Application.UseCases.CreateImmediateTransfer;

public sealed class CreateImmediateTransferValidator : AbstractValidator<CreateImmediateTransferCommand>
{
    public CreateImmediateTransferValidator()
    {
        RuleFor(command => command.SourceAccountId).NotEmpty();
        RuleFor(command => command.DestinationAccountId)
            .NotEmpty()
            .NotEqual(command => command.SourceAccountId).WithMessage("Source and destination accounts must be different.");
        RuleFor(command => command.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, true);
        RuleFor(command => command.IdempotencyKey)
            .Must(idempotencyKey => !string.IsNullOrWhiteSpace(idempotencyKey))
                .WithErrorCode(FieldErrorCodes.InvalidFormat)
                .WithMessage("'Idempotency Key' must not be blank.")
            .MaximumLength(Transfer.IdempotencyKeyMaximumLength)
            .When(command => command.IdempotencyKey is not null);
    }
}
