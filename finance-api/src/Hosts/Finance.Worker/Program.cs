namespace Finance.Worker;

public static class Program
{
    public static async Task Main(string[] args)
    {
        using var host = WorkerHostBuilder.Create(args).Build();
        await host.RunAsync();
    }
}
