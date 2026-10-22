using Finance.Shared.Infrastructure.Messaging;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finance.Shared.Infrastructure.Outbox;

public sealed class OutboxPublisherJob(
    IServiceScopeFactory serviceScopeFactory,
    IMessagePublisher messagePublisher,
    IOptions<OutboxOptions> outboxOptions,
    TimeProvider timeProvider,
    ILogger<OutboxPublisherJob> logger) : BackgroundService
{
    public async Task<int> PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await messagePublisher.EnsureReadyAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The message broker is not ready. Pending outbox messages wait for the next cycle without using attempts.");
            return 0;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var batchSize = outboxOptions.Value.BatchSize;
        var maxAttempts = outboxOptions.Value.MaxAttempts;
        var pendingMessages = await dbContext.Set<OutboxMessage>()
            .FromSql($"SELECT * FROM outbox_messages WHERE processed_at IS NULL AND attempt_count < {maxAttempts} ORDER BY occurred_at LIMIT {batchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        var publishedMessageCount = 0;

        foreach (var pendingMessage in pendingMessages)
        {
            try
            {
                var outgoingMessage = new OutgoingMessage(pendingMessage.Id, pendingMessage.Type, pendingMessage.RoutingKey, pendingMessage.Content);
                await messagePublisher.PublishAsync(outgoingMessage, cancellationToken);
                pendingMessage.MarkAsProcessed(timeProvider.GetUtcNow());
                publishedMessageCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (MessageRejectedException exception)
            {
                pendingMessage.RegisterFailure(exception.Message);
                logger.LogWarning(exception, "The broker rejected outbox message {OutboxMessageId}. The rest of the batch waits for the next cycle.", pendingMessage.Id);
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Publishing outbox message {OutboxMessageId} was interrupted. It waits for the next cycle without using an attempt.", pendingMessage.Id);
                break;
            }
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        return publishedMessageCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(outboxOptions.Value.PollingIntervalSeconds), timeProvider);

        do
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox publishing cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
