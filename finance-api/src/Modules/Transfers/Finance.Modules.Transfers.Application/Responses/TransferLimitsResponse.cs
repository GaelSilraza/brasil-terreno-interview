using Finance.Modules.Transfers.Domain.Enums;

namespace Finance.Modules.Transfers.Application.Responses;

public sealed record TransferLimitsResponse(
    Guid AccountId,
    bool IsCustom,
    TransferPeriod CurrentPeriod,
    PeriodLimitResponse Daytime,
    PeriodLimitResponse Nighttime,
    TransferLimitUsageResponse Usage);
