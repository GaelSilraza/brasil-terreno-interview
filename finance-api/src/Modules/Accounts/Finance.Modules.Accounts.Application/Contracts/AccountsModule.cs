using Finance.Modules.Accounts.Application.Queries;
using Finance.Modules.Accounts.Contracts;
using Finance.Modules.Accounts.Domain.Errors;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Shared.Domain;

namespace Finance.Modules.Accounts.Application.Contracts;

public sealed class AccountsModule(
    IAccountRepository accountRepository,
    IAccountQueries accountQueries,
    TimeProvider timeProvider) : IAccountsModule
{
    public Task<bool> ExistsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return accountRepository.ExistsAsync(accountId, cancellationToken);
    }

    public async Task<Either<Error, TransferParticipants>> FindTransferParticipantsAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        CancellationToken cancellationToken)
    {
        var participants = await accountQueries.FindParticipantsAsync([sourceAccountId, destinationAccountId], cancellationToken);
        var sourceParticipant = participants.FirstOrDefault(participant => participant.AccountId == sourceAccountId);

        if (sourceParticipant is null)
        {
            return AccountErrors.SourceAccountNotFound;
        }

        var destinationParticipant = participants.FirstOrDefault(participant => participant.AccountId == destinationAccountId);

        if (destinationParticipant is null)
        {
            return AccountErrors.DestinationAccountNotFound;
        }

        return new TransferParticipants(sourceParticipant, destinationParticipant);
    }

    public async Task<Either<Error, Unit>> TransferFundsAsync(TransferFundsRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceAccountId == request.DestinationAccountId)
        {
            return AccountErrors.SameAccount;
        }

        var lockedAccounts = await accountRepository.LockForTransferAsync(request.SourceAccountId, request.DestinationAccountId, cancellationToken);
        var source = lockedAccounts.FirstOrDefault(lockedAccount => lockedAccount.Account.Id == request.SourceAccountId);

        if (source is null)
        {
            return AccountErrors.SourceAccountNotFound;
        }

        var destination = lockedAccounts.FirstOrDefault(lockedAccount => lockedAccount.Account.Id == request.DestinationAccountId);

        if (destination is null)
        {
            return AccountErrors.DestinationAccountNotFound;
        }

        return source.Account.TransferTo(
            destination.Account,
            source.Balance,
            destination.Balance,
            request.TransferId,
            request.Amount,
            timeProvider.GetUtcNow());
    }

    public async Task<IReadOnlyDictionary<Guid, string>> FindHolderNamesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        var participants = await accountQueries.FindParticipantsAsync(accountIds, cancellationToken);
        return participants.ToDictionary(participant => participant.AccountId, participant => participant.HolderName);
    }
}
