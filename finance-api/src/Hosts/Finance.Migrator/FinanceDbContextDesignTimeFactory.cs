using Finance.Modules.Accounts.Infrastructure;
using Finance.Modules.Transfers.Infrastructure;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Finance.Migrator;

public sealed class FinanceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Port=55432;Database=finance;Username=finance;Password=finance";

    public FinanceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FinanceDbContext>();
        PersistenceRegistration.ConfigureDbContext(optionsBuilder, DesignTimeConnectionString);
        IModuleDatabaseConfiguration[] moduleDatabaseConfigurations =
        [
            new AccountsDatabaseConfiguration(),
            new TransfersDatabaseConfiguration(),
        ];
        return new FinanceDbContext(optionsBuilder.Options, moduleDatabaseConfigurations);
    }
}
