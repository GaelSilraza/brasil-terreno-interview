using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Domain.Entities;
using Finance.Modules.Accounts.Domain.Enums;
using Finance.Modules.Accounts.Domain.Errors;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Modules.Accounts.Domain.ValueObjects;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Application.Validation;
using Finance.Shared.Domain;
using FluentValidation;

namespace Finance.Modules.Accounts.Application.UseCases.OpenAccount;

public sealed class OpenAccountUseCase(
    IValidator<OpenAccountCommand> validator,
    IPersonRepository personRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Either<Error, AccountResponse>> ExecuteAsync(OpenAccountCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.ToError();
        }

        var documentNumber = DocumentNumberFormat.Normalize(command.DocumentNumber);

        if (await personRepository.ExistsByDocumentNumberAsync(documentNumber, cancellationToken))
        {
            return AccountErrors.DocumentNumberAlreadyExists;
        }

        var now = timeProvider.GetUtcNow();
        var person = Person.Create(command.FullName, documentNumber, now);
        var accountResult = Account.Open(person.Id, command.InitialBalance, command.OverdraftLimit, AccountStatus.Active, now);

        if (accountResult.IsFailure)
        {
            return accountResult.Failure;
        }

        var account = accountResult.Success;
        personRepository.Add(person);
        accountRepository.Add(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AccountResponse.From(account, person, command.InitialBalance);
    }
}
