using Finance.Shared.Domain;

namespace Finance.Modules.Accounts.Contracts;

public interface IAccountsModule
{
    Task<bool> ExistsAsync(Guid accountId, CancellationToken cancellationToken);

    Task<Either<Error, TransferParticipants>> FindTransferParticipantsAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        CancellationToken cancellationToken);

    Task<Either<Error, Unit>> TransferFundsAsync(
        TransferFundsRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> FindHolderNamesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken);
}
