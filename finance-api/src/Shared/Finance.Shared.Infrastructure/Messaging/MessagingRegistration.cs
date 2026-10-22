using Finance.Shared.Application.Messaging;
using Finance.Shared.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finance.Shared.Infrastructure.Messaging;

public static class MessagingRegistration
{
    public static IServiceCollection AddFinanceMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<RabbitMqOptions>().Bind(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName));
        services.TryAddSingleton<RabbitMqConnectionProvider>();
        services.TryAddSingleton<RabbitMqTopologyDeclarer>();
        services.TryAddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        services.AddHostedService<RabbitMqTopologyInitializer>();
        return services;
    }

    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services)
    {
        services.AddSingleton<OutboxPublisherJob>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<OutboxPublisherJob>());
        return services;
    }

    public static IServiceCollection AddRabbitMqConsumer<TMessage, TProcessor>(
        this IServiceCollection services,
        string queueName,
        string bindingKey)
        where TMessage : class
        where TProcessor : class, IMessageProcessor<TMessage>
    {
        services.AddScoped<TProcessor>();
        var consumerQueue = new RabbitMqConsumerQueue(queueName, bindingKey);
        services.AddSingleton(consumerQueue);
        services.AddSingleton(serviceProvider => new RabbitMqConsumerHostedService<TMessage, TProcessor>(
            consumerQueue,
            serviceProvider.GetRequiredService<RabbitMqConnectionProvider>(),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<IOptions<RabbitMqOptions>>(),
            serviceProvider.GetRequiredService<ILogger<RabbitMqConsumerHostedService<TMessage, TProcessor>>>()));
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<RabbitMqConsumerHostedService<TMessage, TProcessor>>());
        return services;
    }
}
