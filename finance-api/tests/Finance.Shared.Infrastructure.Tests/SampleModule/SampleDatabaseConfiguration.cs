using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Shared.Infrastructure.Tests.SampleModule;

public sealed class SampleDatabaseConfiguration : IModuleDatabaseConfiguration
{
    public int ApplyOrder => 1;

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SampleAggregate>(builder =>
        {
            builder.ToTable("samples");
            builder.HasKey(sampleAggregate => sampleAggregate.Id).HasName("pk_samples");
            builder.Property(sampleAggregate => sampleAggregate.Id).ValueGeneratedNever();
            builder.Property(sampleAggregate => sampleAggregate.Name).HasMaxLength(100).IsRequired();
        });
    }
}
