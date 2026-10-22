using Finance.Modules.Transfers.Application.Options;
using Finance.Modules.Transfers.Application.UseCases.DispatchDueScheduledTransfers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finance.Modules.Transfers.Infrastructure.Jobs;

public sealed class ScheduledTransfersDispatcherJob(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ScheduledTransfersOptions> scheduledTransfersOptions,
    TimeProvider timeProvider,
    ILogger<ScheduledTransfersDispatcherJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(scheduledTransfersOptions.Value.PollingIntervalSeconds), timeProvider);

        do
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var dispatchDueScheduledTransfersUseCase = scope.ServiceProvider.GetRequiredService<DispatchDueScheduledTransfersUseCase>();
                var result = await dispatchDueScheduledTransfersUseCase.ExecuteAsync(stoppingToken);

                if (result.StartedTransferCount > 0 || result.TimedOutTransferCount > 0)
                {
                    logger.LogInformation(
                        "Dispatched {StartedTransferCount} scheduled transfers and timed out {TimedOutTransferCount}.",
                        result.StartedTransferCount,
                        result.TimedOutTransferCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled transfers dispatch cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
