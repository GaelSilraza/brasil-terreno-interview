using Finance.Modules.Accounts.Application.Queries;
using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Domain.Errors;
using Finance.Shared.Domain;

namespace Finance.Modules.Accounts.Application.UseCases.GetAccount;

public sealed class GetAccountUseCase(IAccountQueries accountQueries)
{
    public async Task<Either<Error, AccountResponse>> ExecuteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accountQueries.FindByIdAsync(accountId, cancellationToken);

        if (account is null)
        {
            return AccountErrors.AccountNotFound;
        }

        return account;
    }
}
