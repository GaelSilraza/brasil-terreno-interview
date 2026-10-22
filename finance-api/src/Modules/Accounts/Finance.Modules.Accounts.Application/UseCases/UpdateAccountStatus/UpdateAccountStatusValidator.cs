using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.UpdateAccountStatus;

public sealed class UpdateAccountStatusValidator : AbstractValidator<UpdateAccountStatusCommand>
{
    public UpdateAccountStatusValidator()
    {
        RuleFor(command => command.AccountId)
            .NotEmpty();

        RuleFor(command => command.Status)
            .IsInEnum();
    }
}
