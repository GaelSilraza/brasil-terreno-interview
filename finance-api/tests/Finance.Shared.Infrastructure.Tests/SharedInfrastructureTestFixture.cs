using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Infrastructure.Messaging;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Shared.Infrastructure.Tests.SampleModule;
using Finance.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Shared.Infrastructure.Tests;

public sealed class SharedInfrastructureTestFixture : FinanceTestFixture
{
    protected override bool UsesRabbitMq => true;

    protected override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleDatabaseConfiguration, SampleDatabaseConfiguration>();
        services.AddScoped<IDomainEventHandler, SampleCreatedDomainEventHandler>();
        services.AddSingleton<ReceivedSampleMessages>();
        services.AddFinanceMessaging(configuration);
        services.AddOutboxPublisher();
        services.AddRabbitMqConsumer<SampleMessage, SampleMessageProcessor>("testing.samples", "testing.#");
    }
}
