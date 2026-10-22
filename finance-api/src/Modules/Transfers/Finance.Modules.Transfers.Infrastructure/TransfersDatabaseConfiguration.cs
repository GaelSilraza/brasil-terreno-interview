using Finance.Modules.Transfers.Infrastructure.Persistence.Configurations;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finance.Modules.Transfers.Infrastructure;

public sealed class TransfersDatabaseConfiguration : IModuleDatabaseConfiguration
{
    public int ApplyOrder => 2;

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TransferConfiguration());
        modelBuilder.ApplyConfiguration(new TransferLimitPolicyConfiguration());
    }
}
