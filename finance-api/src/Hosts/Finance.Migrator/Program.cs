using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Finance.Migrator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        DatabaseInitializer.Register(builder.Services, builder.Configuration);

        using var host = builder.Build();
        var databaseInitializer = host.Services.GetRequiredService<DatabaseInitializer>();
        await databaseInitializer.InitializeAsync(CancellationToken.None);
    }
}
