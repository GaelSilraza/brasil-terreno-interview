using Finance.Modules.Accounts.Infrastructure.Persistence.Configurations;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Modules.Accounts.Infrastructure;

public sealed class AccountsDatabaseConfiguration : IModuleDatabaseConfiguration
{
    public int ApplyOrder => 1;

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfiguration(new PersonConfiguration());
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new AccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AccountBalanceConfiguration());
    }
}
