using Finance.Modules.Transfers.Application;
using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Application.Options;
using Finance.Modules.Transfers.Application.Processors;
using Finance.Modules.Transfers.Application.Queries;
using Finance.Modules.Transfers.Domain.Repositories;
using Finance.Modules.Transfers.Infrastructure.Jobs;
using Finance.Modules.Transfers.Infrastructure.Persistence.Queries;
using Finance.Modules.Transfers.Infrastructure.Persistence.Repositories;
using Finance.Shared.Infrastructure.Messaging;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Transfers.Infrastructure;

public static class TransfersModuleRegistration
{
    public static IServiceCollection AddTransfersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TransferLimitsOptions>().Bind(configuration.GetSection(TransferLimitsOptions.SectionName));
        services.AddOptions<ScheduledTransfersOptions>().Bind(configuration.GetSection(ScheduledTransfersOptions.SectionName));
        services.AddSingleton<IModuleDatabaseConfiguration, TransfersDatabaseConfiguration>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<ITransferLimitPolicyRepository, TransferLimitPolicyRepository>();
        services.AddScoped<ITransferQueries, TransferQueries>();
        services.AddTransfersApplication();
        return services;
    }

    public static IServiceCollection AddTransfersBackgroundProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFinanceMessaging(configuration);
        services.AddHostedService<ScheduledTransfersDispatcherJob>();
        services.AddRabbitMqConsumer<TransferEventMessage, ExecuteScheduledTransferProcessor>(
            TransferQueues.ScheduledExecution,
            TransferRoutingKeys.ProcessingStarted);
        services.AddRabbitMqConsumer<TransferEventMessage, TransferNotificationProcessor>(
            TransferQueues.Notifications,
            TransferRoutingKeys.AllTransferEvents);
        return services;
    }
}
