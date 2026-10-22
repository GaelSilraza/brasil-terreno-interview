using Finance.Modules.Accounts.Infrastructure;
using Finance.Modules.Transfers.Infrastructure;
using Finance.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Transfers.Tests;

public sealed class TransfersTestFixture : FinanceTestFixture
{
    protected override bool UsesMigrations => true;

    protected override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAccountsModule(configuration);
        services.AddTransfersModule(configuration);
    }
}
