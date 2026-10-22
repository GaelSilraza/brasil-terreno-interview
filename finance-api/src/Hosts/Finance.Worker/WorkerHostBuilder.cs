using Finance.Modules.Accounts.Infrastructure;
using Finance.Modules.Transfers.Infrastructure;
using Finance.Shared.Infrastructure.Messaging;
using Finance.Shared.Infrastructure.Persistence;

namespace Finance.Worker;

public static class WorkerHostBuilder
{
    public static HostApplicationBuilder Create(string[] args, IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        var builder = Host.CreateApplicationBuilder(args);

        if (configurationOverrides is not null)
        {
            builder.Configuration.AddInMemoryCollection(configurationOverrides);
        }

        builder.Services.AddFinancePersistence(builder.Configuration);
        builder.Services.AddAccountsModule(builder.Configuration);
        builder.Services.AddTransfersModule(builder.Configuration);
        builder.Services.AddTransfersBackgroundProcessing(builder.Configuration);
        builder.Services.AddOutboxPublisher();
        return builder;
    }
}
