using Finance.Modules.Transfers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Modules.Transfers.Infrastructure.Persistence.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers", table =>
        {
            table.HasCheckConstraint("ck_transfers_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_transfers_source_differs_from_destination", "source_account_id <> destination_account_id");
        });
        builder.HasKey(transfer => transfer.Id).HasName("pk_transfers");
        builder.Property(transfer => transfer.Id).ValueGeneratedNever();
        builder.Property(transfer => transfer.Amount).HasPrecision(18, 2);
        builder.Property(transfer => transfer.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(transfer => transfer.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(transfer => transfer.FailureCode).HasMaxLength(Transfer.FailureCodeMaximumLength);
        builder.Property(transfer => transfer.IdempotencyKey).HasMaxLength(Transfer.IdempotencyKeyMaximumLength);
        builder.Property(transfer => transfer.RequestFingerprint).HasMaxLength(Transfer.RequestFingerprintLength);

        builder.HasOne(AccountsEntityNames.Account, navigationName: null)
            .WithMany()
            .HasForeignKey(nameof(Transfer.SourceAccountId))
            .HasConstraintName("fk_transfers_accounts_source_account_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(AccountsEntityNames.Account, navigationName: null)
            .WithMany()
            .HasForeignKey(nameof(Transfer.DestinationAccountId))
            .HasConstraintName("fk_transfers_accounts_destination_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(transfer => new { transfer.SourceAccountId, transfer.AttemptedAt })
            .HasDatabaseName("ix_transfers_source_account_id_attempted_at");
        builder.HasIndex(transfer => new { transfer.DestinationAccountId, transfer.Id })
            .HasDatabaseName("ix_transfers_destination_account_id_id");
        builder.HasIndex(transfer => new { transfer.SourceAccountId, transfer.Id })
            .HasDatabaseName("ix_transfers_source_account_id_id");
        builder.HasIndex(transfer => transfer.ScheduledFor)
            .HasDatabaseName("ix_transfers_scheduled_for_due")
            .HasFilter("status = 'Scheduled'");
        builder.HasIndex(transfer => transfer.ProcessingStartedAt)
            .HasDatabaseName("ix_transfers_processing_started_at_stale")
            .HasFilter("status = 'Processing'");
        builder.HasIndex(transfer => transfer.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_transfers_idempotency_key")
            .HasFilter("idempotency_key IS NOT NULL");
    }
}
