using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Shared.Infrastructure.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(outboxMessage => outboxMessage.Id).HasName("pk_outbox_messages");
        builder.Property(outboxMessage => outboxMessage.Id).ValueGeneratedNever();
        builder.Property(outboxMessage => outboxMessage.Type).HasMaxLength(200).IsRequired();
        builder.Property(outboxMessage => outboxMessage.RoutingKey).HasMaxLength(200).IsRequired();
        builder.Property(outboxMessage => outboxMessage.Content).HasColumnType("jsonb").IsRequired();
        builder.Property(outboxMessage => outboxMessage.OccurredAt).IsRequired();
        builder.Property(outboxMessage => outboxMessage.AttemptCount).IsRequired();
        builder.Property(outboxMessage => outboxMessage.LastError).HasMaxLength(OutboxMessage.LastErrorMaximumLength);
        builder.HasIndex(outboxMessage => outboxMessage.OccurredAt)
            .HasDatabaseName("ix_outbox_messages_unprocessed")
            .HasFilter("processed_at IS NULL");
    }
}
