using Finance.Modules.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Modules.Accounts.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", table =>
            table.HasCheckConstraint("ck_accounts_overdraft_limit_non_negative", "overdraft_limit >= 0"));
        builder.HasKey(account => account.Id).HasName("pk_accounts");
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.OverdraftLimit).HasPrecision(18, 2);
        builder.Property(account => account.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(account => account.IsActive);

        builder.HasOne<Person>()
            .WithOne()
            .HasForeignKey<Account>(account => account.PersonId)
            .HasConstraintName("fk_accounts_persons_person_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(account => account.PersonId).IsUnique().HasDatabaseName("ux_accounts_person_id");

        builder.HasMany(account => account.Entries)
            .WithOne()
            .HasForeignKey(entry => entry.AccountId)
            .HasConstraintName("fk_account_entries_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(account => account.Entries)
            .HasField("entries")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
