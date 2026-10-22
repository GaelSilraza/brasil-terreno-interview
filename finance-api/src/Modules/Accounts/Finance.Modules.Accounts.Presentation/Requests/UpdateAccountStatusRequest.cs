using Finance.Modules.Accounts.Domain.Enums;

namespace Finance.Modules.Accounts.Presentation.Requests;

public sealed record UpdateAccountStatusRequest(AccountStatus Status);
