using Finance.Modules.Accounts.Domain.Errors;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.UpdateAccountStatus;

public sealed class UpdateAccountStatusUseCase(
    IValidator<UpdateAccountStatusCommand> validator,
    IAccountRepository accountRepository,
    TimeProvider timeProvider)
{
    public async Task<Either<Error, Unit>> ExecuteAsync(UpdateAccountStatusCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var affectedRows = await accountRepository.UpdateStatusAsync(
            command.AccountId,
            command.Status,
            timeProvider.GetUtcNow(),
            cancellationToken);

        if (affectedRows == 0)
        {
            return AccountErrors.AccountNotFound;
        }

        return Unit.Value;
    }
}
