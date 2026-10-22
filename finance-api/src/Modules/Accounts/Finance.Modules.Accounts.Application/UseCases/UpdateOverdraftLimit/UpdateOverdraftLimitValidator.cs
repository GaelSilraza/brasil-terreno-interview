using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.UpdateOverdraftLimit;

public sealed class UpdateOverdraftLimitValidator : AbstractValidator<UpdateOverdraftLimitCommand>
{
    public UpdateOverdraftLimitValidator()
    {
        RuleFor(command => command.AccountId)
            .NotEmpty();

        RuleFor(command => command.OverdraftLimit)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 2, true);
    }
}
