using Finance.Modules.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Modules.Accounts.Infrastructure.Persistence.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons");
        builder.HasKey(person => person.Id).HasName("pk_persons");
        builder.Property(person => person.Id).ValueGeneratedNever();
        builder.Property(person => person.FullName).HasMaxLength(Person.FullNameMaximumLength).IsRequired();
        builder.Property(person => person.DocumentNumber).HasMaxLength(Person.DocumentNumberLength).IsRequired();
        builder.HasIndex(person => person.DocumentNumber)
            .IsUnique()
            .HasDatabaseName("ux_persons_document_number")
            .HasOperators("varchar_pattern_ops");
        builder.HasIndex(person => new { person.FullName, person.Id })
            .HasDatabaseName("ix_persons_full_name_id");
        builder.HasIndex(person => person.FullName, "ix_persons_full_name_trgm")
            .HasDatabaseName("ix_persons_full_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
