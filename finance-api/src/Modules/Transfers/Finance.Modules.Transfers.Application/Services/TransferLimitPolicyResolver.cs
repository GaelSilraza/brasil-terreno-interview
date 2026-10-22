using Finance.Modules.Transfers.Application.Options;
using Finance.Modules.Transfers.Domain.Entities;
using Finance.Modules.Transfers.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Finance.Modules.Transfers.Application.Services;

public sealed class TransferLimitPolicyResolver(
    ITransferLimitPolicyRepository transferLimitPolicyRepository,
    IOptions<TransferLimitsOptions> transferLimitsOptions,
    TimeProvider timeProvider)
{
    public async Task<ResolvedTransferLimitPolicy> ResolveAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var customPolicy = await transferLimitPolicyRepository.FindByAccountIdAsync(accountId, cancellationToken);

        if (customPolicy is not null)
        {
            return new ResolvedTransferLimitPolicy(customPolicy, IsCustom: true);
        }

        var defaults = transferLimitsOptions.Value;
        var defaultPolicy = TransferLimitPolicy.Create(
            accountId,
            defaults.DefaultDaytimeMaxAmountPerHour,
            defaults.DefaultDaytimeMaxAttemptsPerHour,
            defaults.DefaultNighttimeMaxAmountPerHour,
            defaults.DefaultNighttimeMaxAttemptsPerHour,
            timeProvider.GetUtcNow());

        return new ResolvedTransferLimitPolicy(defaultPolicy, IsCustom: false);
    }
}
