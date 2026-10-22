using Finance.Modules.Transfers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Modules.Transfers.Infrastructure.Persistence.Configurations;

public sealed class TransferLimitPolicyConfiguration : IEntityTypeConfiguration<TransferLimitPolicy>
{
    public void Configure(EntityTypeBuilder<TransferLimitPolicy> builder)
    {
        builder.ToTable("transfer_limit_policies");
        builder.HasKey(policy => policy.AccountId).HasName("pk_transfer_limit_policies");
        builder.Property(policy => policy.AccountId).ValueGeneratedNever();
        builder.Property(policy => policy.DaytimeMaxAmountPerHour).HasPrecision(18, 2);
        builder.Property(policy => policy.NighttimeMaxAmountPerHour).HasPrecision(18, 2);
        builder.HasOne(AccountsEntityNames.Account, navigationName: null)
            .WithMany()
            .HasForeignKey(nameof(TransferLimitPolicy.AccountId))
            .HasConstraintName("fk_transfer_limit_policies_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
