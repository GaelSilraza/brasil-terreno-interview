using Finance.Modules.Accounts.Domain.Enums;

namespace Finance.Modules.Accounts.Application.UseCases.UpdateAccountStatus;

public sealed record UpdateAccountStatusCommand(Guid AccountId, AccountStatus Status);
