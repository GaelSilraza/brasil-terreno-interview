using FluentValidation;

namespace Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;

public sealed class SetTransferLimitsValidator : AbstractValidator<SetTransferLimitsCommand>
{
    public SetTransferLimitsValidator()
    {
        RuleFor(command => command.AccountId).NotEmpty();
        RuleFor(command => command.DaytimeMaxAmountPerHour)
            .GreaterThan(0)
            .PrecisionScale(18, 2, true);
        RuleFor(command => command.NighttimeMaxAmountPerHour)
            .GreaterThan(0)
            .PrecisionScale(18, 2, true);
        RuleFor(command => command.DaytimeMaxAttemptsPerHour).InclusiveBetween(1, 1000);
        RuleFor(command => command.NighttimeMaxAttemptsPerHour).InclusiveBetween(1, 1000);
    }
}
