using Finance.Modules.Transfers.Domain.Entities;

namespace Finance.Modules.Transfers.Application.Services;

public sealed record ResolvedTransferLimitPolicy(TransferLimitPolicy Policy, bool IsCustom);
