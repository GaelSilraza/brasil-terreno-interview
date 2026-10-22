using Finance.Modules.Accounts.Domain.Errors;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.UpdateOverdraftLimit;

public sealed class UpdateOverdraftLimitUseCase(
    IValidator<UpdateOverdraftLimitCommand> validator,
    IAccountRepository accountRepository,
    TimeProvider timeProvider)
{
    public async Task<Either<Error, Unit>> ExecuteAsync(UpdateOverdraftLimitCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var affectedRows = await accountRepository.UpdateOverdraftLimitAsync(
            command.AccountId,
            command.OverdraftLimit,
            timeProvider.GetUtcNow(),
            cancellationToken);

        if (affectedRows == 0)
        {
            return AccountErrors.AccountNotFound;
        }

        return Unit.Value;
    }
}
