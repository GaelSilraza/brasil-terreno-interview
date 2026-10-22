using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.ValueObjects;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.OpenAccount;

public sealed class OpenAccountValidator : AbstractValidator<OpenAccountCommand>
{
    public OpenAccountValidator()
    {
        RuleFor(command => command.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(fullName => fullName.Trim().Length >= 3)
                .WithErrorCode(FieldErrorCodes.InvalidLength)
                .WithMessage("'Full Name' must have at least 3 characters.")
            .MaximumLength(Person.FullNameMaximumLength);

        RuleFor(command => command.DocumentNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(DocumentNumberFormat.IsAccepted)
                .WithErrorCode(FieldErrorCodes.InvalidFormat)
                .WithMessage("'Document Number' must have 11 digits, with or without the mask 000.000.000-00.");

        RuleFor(command => command.InitialBalance)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 2, true);

        RuleFor(command => command.OverdraftLimit)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 2, true);
    }
}
