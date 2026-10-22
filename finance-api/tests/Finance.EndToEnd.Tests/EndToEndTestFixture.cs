using Finance.Migrator;
using Finance.Modules.Accounts.Infrastructure;
using Finance.Modules.Transfers.Infrastructure;
using Finance.Testing;
using Finance.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Finance.EndToEnd.Tests;

public sealed class EndToEndTestFixture : FinanceTestFixture
{
    private FinanceApiFactory? apiFactory;
    private HttpClient? httpClient;
    private IHost? workerHost;

    public ControllableTimeProvider Clock { get; } = new(TestClock.DaytimeInSaoPaulo);

    public HttpClient HttpClient => httpClient ?? throw new InvalidOperationException("Call StartHostsAsync before using the HttpClient.");

    protected override bool UsesRabbitMq => true;

    public async ValueTask StartHostsAsync()
    {
        if (apiFactory is not null)
        {
            return;
        }

        var configurationValues = CreateConfigurationValues();
        configurationValues["OpenApi:Enabled"] = "true";
        configurationValues["ScheduledTransfers:PollingIntervalSeconds"] = "1";
        configurationValues["Outbox:PollingIntervalSeconds"] = "1";

        apiFactory = new FinanceApiFactory(configurationValues, Clock);
        httpClient = apiFactory.CreateClient();

        var workerHostBuilder = WorkerHostBuilder.Create([], configurationValues);
        workerHostBuilder.Services.RemoveAll<TimeProvider>();
        workerHostBuilder.Services.AddSingleton<TimeProvider>(Clock);
        workerHost = workerHostBuilder.Build();
        await workerHost.StartAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (workerHost is not null)
        {
            await workerHost.StopAsync();
            workerHost.Dispose();
        }

        if (apiFactory is not null)
        {
            await apiFactory.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    protected override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAccountsModule(configuration);
        services.AddTransfersModule(configuration);
    }

    protected override async Task PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(CreateConfigurationValues()).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        DatabaseInitializer.Register(services, configuration);
        await using var serviceProvider = services.BuildServiceProvider();
        await serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
    }
}
