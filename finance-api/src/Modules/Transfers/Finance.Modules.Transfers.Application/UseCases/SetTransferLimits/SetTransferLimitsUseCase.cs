using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Errors;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;

public sealed class SetTransferLimitsUseCase(
    IValidator<SetTransferLimitsCommand> validator,
    IAccountsModule accountsModule,
    ITransferLimitPolicyRepository transferLimitPolicyRepository,
    TimeProvider timeProvider)
{
    public async Task<Either<Error, Unit>> ExecuteAsync(SetTransferLimitsCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var accountExists = await accountsModule.ExistsAsync(command.AccountId, cancellationToken);

        if (!accountExists)
        {
            return TransferErrors.AccountNotFound;
        }

        var transferLimitPolicy = TransferLimitPolicy.Create(
            command.AccountId,
            command.DaytimeMaxAmountPerHour,
            command.DaytimeMaxAttemptsPerHour,
            command.NighttimeMaxAmountPerHour,
            command.NighttimeMaxAttemptsPerHour,
            timeProvider.GetUtcNow());

        await transferLimitPolicyRepository.UpsertAsync(transferLimitPolicy, cancellationToken);
        return Unit.Value;
    }
}
