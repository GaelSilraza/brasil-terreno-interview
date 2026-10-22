using Finance.Modules.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Modules.Accounts.Infrastructure.Persistence.Configurations;

public sealed class AccountEntryConfiguration : IEntityTypeConfiguration<AccountEntry>
{
    public void Configure(EntityTypeBuilder<AccountEntry> builder)
    {
        builder.ToTable("account_entries");
        builder.HasKey(entry => entry.Id).HasName("pk_account_entries");
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Amount).HasPrecision(18, 2);
        builder.Property(entry => entry.BalanceAfter).HasPrecision(18, 2);
        builder.Property(entry => entry.EntryType).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(entry => new { entry.AccountId, entry.Id })
            .HasDatabaseName("ix_account_entries_account_id_id")
            .IncludeProperties(entry => new { entry.EntryType, entry.Amount });
        builder.HasIndex(entry => entry.AccountId, "ux_account_entries_opening")
            .IsUnique()
            .HasDatabaseName("ux_account_entries_opening")
            .HasFilter("transfer_id IS NULL");
        builder.HasIndex(entry => new { entry.TransferId, entry.EntryType }).IsUnique().HasDatabaseName("ux_account_entries_transfer_id_entry_type");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(entry => entry.CounterpartAccountId)
            .HasConstraintName("fk_account_entries_accounts_counterpart_account_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entry => entry.CounterpartAccountId).HasDatabaseName("ix_account_entries_counterpart_account_id");
    }
}
