using System.Diagnostics;
using Finance.Shared.Application.Persistence;
using Finance.Shared.Infrastructure.Messaging;
using Finance.Shared.Infrastructure.Outbox;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Shared.Infrastructure.Tests.SampleModule;
using Finance.Shared.Infrastructure.Tests.Support;
using Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Finance.Shared.Infrastructure.Tests;

public sealed class MessagingFlowTests(SharedInfrastructureTestFixture fixture) : IAsyncLifetime
{
    private const string SampleQueueName = "testing.samples";

    private const string SampleBindingKey = "testing.#";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(20);

    private readonly FinanceTestEnvironment environment = fixture.CreateEnvironment();

    public async ValueTask InitializeAsync()
    {
        await environment.ResetDatabaseAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await environment.DisposeAsync();
    }

    [Fact]
    public async Task PublishedOutboxMessage_IsDeliveredToTheProcessor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareSampleQueueAsync(cancellationToken);
        var consumer = environment.Services.GetRequiredService<RabbitMqConsumerHostedService<SampleMessage, SampleMessageProcessor>>();
        var outboxPublisherJob = environment.Services.GetRequiredService<OutboxPublisherJob>();
        var receivedSampleMessages = environment.Services.GetRequiredService<ReceivedSampleMessages>();
        await consumer.StartAsync(cancellationToken);
        var sampleAggregate = await SaveSampleAsync("delivered sample", cancellationToken);

        var publishedMessageCount = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);
        var deliveredMessages = await Eventually.WaitUntilAsync(
            _ => Task.FromResult(receivedSampleMessages.All),
            messages => messages.Any(message => message.SampleId == sampleAggregate.Id),
            DeliveryTimeout,
            cancellationToken);
        await consumer.StopAsync(cancellationToken);

        Assert.Equal(1, publishedMessageCount);
        var deliveredMessage = Assert.Single(deliveredMessages, message => message.SampleId == sampleAggregate.Id);
        Assert.Equal("delivered sample", deliveredMessage.Name);
        var outboxMessage = await FindSingleOutboxMessageAsync(cancellationToken);
        Assert.NotNull(outboxMessage.ProcessedAt);
        Assert.Equal(0, outboxMessage.AttemptCount);
    }

    [Fact]
    public async Task MessagePublishedBeforeTheConsumerStarts_IsDeliveredOnceItStarts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeleteSampleQueueAsync(cancellationToken);
        var consumer = environment.Services.GetRequiredService<RabbitMqConsumerHostedService<SampleMessage, SampleMessageProcessor>>();
        var outboxPublisherJob = environment.Services.GetRequiredService<OutboxPublisherJob>();
        var receivedSampleMessages = environment.Services.GetRequiredService<ReceivedSampleMessages>();
        var sampleAggregate = await SaveSampleAsync("published before the consumer", cancellationToken);

        var publishedMessageCount = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);
        await consumer.StartAsync(cancellationToken);
        var deliveredMessages = await Eventually.WaitUntilAsync(
            _ => Task.FromResult(receivedSampleMessages.All),
            messages => messages.Any(message => message.SampleId == sampleAggregate.Id),
            DeliveryTimeout,
            cancellationToken);
        await consumer.StopAsync(cancellationToken);

        Assert.Equal(1, publishedMessageCount);
        var deliveredMessage = Assert.Single(deliveredMessages, message => message.SampleId == sampleAggregate.Id);
        Assert.Equal("published before the consumer", deliveredMessage.Name);
    }

    [Fact]
    public async Task ConsumerQueueDeclaredWithOtherArguments_KeepsTheMessagePendingWithoutUsingAttemptsUntilTheQueueIsFixed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeleteSampleQueueAsync(cancellationToken);
        await DeclareSampleQueueWithoutDeadLetteringAsync(cancellationToken);
        var outboxPublisherJob = environment.Services.GetRequiredService<OutboxPublisherJob>();
        await SaveSampleAsync("held back by a conflicting queue", cancellationToken);

        var maxAttempts = environment.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.MaxAttempts;

        var publishedWhileConflicting = 0;

        for (var cycle = 0; cycle <= maxAttempts; cycle++)
        {
            publishedWhileConflicting += await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);
        }

        var pendingMessage = await FindSingleOutboxMessageAsync(cancellationToken);
        await DeleteSampleQueueAsync(cancellationToken);
        var publishedAfterTheFix = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);

        Assert.Equal(0, publishedWhileConflicting);
        Assert.Null(pendingMessage.ProcessedAt);
        Assert.Equal(0, pendingMessage.AttemptCount);
        Assert.Null(pendingMessage.LastError);
        Assert.Equal(1, publishedAfterTheFix);
        var publishedMessage = await FindSingleOutboxMessageAsync(cancellationToken);
        Assert.NotNull(publishedMessage.ProcessedAt);
    }

    [Fact]
    public async Task StartupWithAConsumerQueueDeclaredWithOtherArguments_Fails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeleteSampleQueueAsync(cancellationToken);
        await DeclareSampleQueueWithoutDeadLetteringAsync(cancellationToken);
        var topologyInitializer = environment.Services.GetServices<IHostedService>().OfType<RabbitMqTopologyInitializer>().Single();

        try
        {
            await Assert.ThrowsAsync<OperationInterruptedException>(() => topologyInitializer.StartAsync(cancellationToken));
        }
        finally
        {
            await DeleteSampleQueueAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task StartupWithAFreshBroker_DeclaresEveryConsumerQueue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeleteSampleQueueAsync(cancellationToken);
        var topologyInitializer = environment.Services.GetServices<IHostedService>().OfType<RabbitMqTopologyInitializer>().Single();

        await topologyInitializer.StartAsync(cancellationToken);

        var rabbitMqConnectionProvider = environment.Services.GetRequiredService<RabbitMqConnectionProvider>();
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var declaredQueue = await channel.QueueDeclarePassiveAsync(SampleQueueName, cancellationToken);
        Assert.Equal(SampleQueueName, declaredQueue.QueueName);
    }

    [Fact]
    public async Task StartupWhileBrokerIsUnreachable_DoesNotStopTheHost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var unreachableBrokerOverrides = new Dictionary<string, string?>
        {
            ["RabbitMq:Port"] = "1",
            ["RabbitMq:ConnectionAttempts"] = "1",
        };
        await using var unreachableBrokerEnvironment = fixture.CreateEnvironment(unreachableBrokerOverrides);
        var topologyInitializer = unreachableBrokerEnvironment.Services.GetServices<IHostedService>().OfType<RabbitMqTopologyInitializer>().Single();

        var startupException = await Record.ExceptionAsync(() => topologyInitializer.StartAsync(cancellationToken));

        Assert.Null(startupException);
    }

    [Fact]
    public async Task PublishInterruptedByTheTransport_WaitsForTheNextCycleWithoutUsingAnAttempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareSampleQueueAsync(cancellationToken);
        var interruptingPublisher = new InterruptingMessagePublisher(
            environment.Services.GetRequiredService<IMessagePublisher>(),
            publishNumber => publishNumber == 1 ? Task.FromException(new IOException("Connection reset by the test.")) : Task.CompletedTask);
        var outboxPublisherJob = CreateOutboxPublisherJob(interruptingPublisher);
        await SaveSampleAsync("interrupted by the transport", cancellationToken);

        var publishedWhenInterrupted = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);
        var interruptedMessage = await FindSingleOutboxMessageAsync(cancellationToken);
        var publishedOnTheNextCycle = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);

        Assert.Equal(0, publishedWhenInterrupted);
        Assert.Null(interruptedMessage.ProcessedAt);
        Assert.Equal(0, interruptedMessage.AttemptCount);
        Assert.Null(interruptedMessage.LastError);
        Assert.Equal(1, publishedOnTheNextCycle);
        var publishedMessage = await FindSingleOutboxMessageAsync(cancellationToken);
        Assert.NotNull(publishedMessage.ProcessedAt);
    }

    [Fact]
    public async Task ShutdownDuringABatch_KeepsTheMessagesAlreadyPublishedAsProcessed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareSampleQueueAsync(cancellationToken);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var interruptingPublisher = new InterruptingMessagePublisher(
            environment.Services.GetRequiredService<IMessagePublisher>(),
            publishNumber => publishNumber == 2 ? shutdown.CancelAsync() : Task.CompletedTask);
        var outboxPublisherJob = CreateOutboxPublisherJob(interruptingPublisher);
        await SaveSampleAsync("published before the shutdown", cancellationToken);
        await SaveSampleAsync("pending at the shutdown", cancellationToken);

        var publishedMessageCount = await outboxPublisherJob.PublishPendingMessagesAsync(shutdown.Token);

        Assert.Equal(1, publishedMessageCount);
        var outboxMessages = await FindOutboxMessagesAsync(environment, cancellationToken);
        Assert.Single(outboxMessages, outboxMessage => outboxMessage.ProcessedAt is not null);
        var pendingMessage = Assert.Single(outboxMessages, outboxMessage => outboxMessage.ProcessedAt is null);
        Assert.Equal(0, pendingMessage.AttemptCount);
    }

    [Fact]
    public async Task ConsumerQueueDeletedWhileThePublishingChannelIsOpen_IsDeclaredAgainOnTheNextCycle()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareSampleQueueAsync(cancellationToken);
        var outboxPublisherJob = environment.Services.GetRequiredService<OutboxPublisherJob>();
        await SaveSampleAsync("published while the queue exists", cancellationToken);
        await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);
        await DeleteSampleQueueAsync(cancellationToken);
        await SaveSampleAsync("published after the queue was deleted", cancellationToken);

        var publishedWithoutTheQueue = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);
        var publishedOnTheNextCycle = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);

        Assert.Equal(0, publishedWithoutTheQueue);
        Assert.Equal(1, publishedOnTheNextCycle);
        var outboxMessages = await FindOutboxMessagesAsync(environment, cancellationToken);
        Assert.All(outboxMessages, outboxMessage => Assert.NotNull(outboxMessage.ProcessedAt));
        Assert.Single(outboxMessages, outboxMessage => outboxMessage.AttemptCount == 1);
    }

    [Fact]
    public async Task ConsumerQueueDeletedWhileConsuming_ConsumerStartsAgainAndReceivesNewMessages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareSampleQueueAsync(cancellationToken);
        var consumer = environment.Services.GetRequiredService<RabbitMqConsumerHostedService<SampleMessage, SampleMessageProcessor>>();
        var outboxPublisherJob = environment.Services.GetRequiredService<OutboxPublisherJob>();
        var receivedSampleMessages = environment.Services.GetRequiredService<ReceivedSampleMessages>();
        await consumer.StartAsync(cancellationToken);
        await WaitUntilTheSampleQueueHasAConsumerAsync(cancellationToken);

        await DeleteSampleQueueAsync(cancellationToken);
        var sampleAggregate = await SaveSampleAsync("sent after the queue was deleted", cancellationToken);
        var deliveredMessages = await PublishUntilDeliveredAsync(outboxPublisherJob, receivedSampleMessages, sampleAggregate.Id, cancellationToken);
        await consumer.StopAsync(cancellationToken);

        Assert.Single(deliveredMessages, message => message.SampleId == sampleAggregate.Id);
    }

    [Fact]
    public async Task ConnectionClosedByTheBroker_ConsumerAndOutboxResumeWithoutKeepingTheOldConnectionOpen()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareSampleQueueAsync(cancellationToken);
        var consumer = environment.Services.GetRequiredService<RabbitMqConsumerHostedService<SampleMessage, SampleMessageProcessor>>();
        var outboxPublisherJob = environment.Services.GetRequiredService<OutboxPublisherJob>();
        var receivedSampleMessages = environment.Services.GetRequiredService<ReceivedSampleMessages>();
        var rabbitMqConnectionProvider = environment.Services.GetRequiredService<RabbitMqConnectionProvider>();
        await consumer.StartAsync(cancellationToken);
        await WaitUntilTheSampleQueueHasAConsumerAsync(cancellationToken);
        var connectionBeforeTheClose = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);

        await fixture.CloseRabbitMqConnectionsAsync(cancellationToken);
        var sampleAggregate = await SaveSampleAsync("sent after the broker closed the connection", cancellationToken);
        var deliveredMessages = await PublishUntilDeliveredAsync(outboxPublisherJob, receivedSampleMessages, sampleAggregate.Id, cancellationToken);
        var connectionAfterTheClose = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);
        await consumer.StopAsync(cancellationToken);

        Assert.Single(deliveredMessages, message => message.SampleId == sampleAggregate.Id);
        Assert.True(ReferenceEquals(connectionBeforeTheClose, connectionAfterTheClose) || !connectionBeforeTheClose.IsOpen);
    }

    [Fact]
    public async Task MessageThatFailsProcessing_IsMovedToTheDeadLetterQueue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DeclareSampleQueueAsync(cancellationToken);
        var consumer = environment.Services.GetRequiredService<RabbitMqConsumerHostedService<SampleMessage, SampleMessageProcessor>>();
        var outboxPublisherJob = environment.Services.GetRequiredService<OutboxPublisherJob>();
        var receivedSampleMessages = environment.Services.GetRequiredService<ReceivedSampleMessages>();
        var rabbitMqConnectionProvider = environment.Services.GetRequiredService<RabbitMqConnectionProvider>();
        var deadLetterQueueName = environment.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value.DeadLetterQueueName;
        await consumer.StartAsync(cancellationToken);
        var sampleAggregate = await SaveSampleAsync(SampleMessageProcessor.PoisonName, cancellationToken);

        await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var deadLetterMessageCount = await Eventually.WaitUntilAsync(
            probeCancellationToken => channel.MessageCountAsync(deadLetterQueueName, probeCancellationToken),
            messageCount => messageCount >= 1,
            DeliveryTimeout,
            cancellationToken);
        await consumer.StopAsync(cancellationToken);

        Assert.True(deadLetterMessageCount >= 1);
        Assert.DoesNotContain(receivedSampleMessages.All, message => message.SampleId == sampleAggregate.Id);
        var outboxMessage = await FindSingleOutboxMessageAsync(cancellationToken);
        Assert.NotNull(outboxMessage.ProcessedAt);
    }

    [Fact]
    public async Task PublishingWhileBrokerIsUnreachable_KeepsEveryMessagePendingWithoutUsingAttempts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var unreachableBrokerOverrides = new Dictionary<string, string?>
        {
            ["RabbitMq:Port"] = "1",
            ["RabbitMq:ConnectionAttempts"] = "1",
        };
        await using var unreachableBrokerEnvironment = fixture.CreateEnvironment(unreachableBrokerOverrides);
        var outboxPublisherJob = unreachableBrokerEnvironment.Services.GetRequiredService<OutboxPublisherJob>();
        await SaveSampleAsync(unreachableBrokerEnvironment, "first pending sample", cancellationToken);
        await SaveSampleAsync(unreachableBrokerEnvironment, "second pending sample", cancellationToken);

        var publishedMessageCount = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);

        Assert.Equal(0, publishedMessageCount);
        var outboxMessages = await FindOutboxMessagesAsync(unreachableBrokerEnvironment, cancellationToken);
        Assert.Equal(2, outboxMessages.Count);
        Assert.All(outboxMessages, outboxMessage =>
        {
            Assert.Null(outboxMessage.ProcessedAt);
            Assert.Equal(0, outboxMessage.AttemptCount);
            Assert.Null(outboxMessage.LastError);
        });
    }

    [Fact]
    public async Task MessageThatNoQueueIsBoundTo_IsNotMarkedAsProcessed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var outboxPublisherJob = environment.Services.GetRequiredService<OutboxPublisherJob>();
        await using (var scope = environment.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            dbContext.Set<OutboxMessage>().Add(OutboxMessage.Create(nameof(SampleMessage), "unbound.sample-created", "{}", environment.TimeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var publishedMessageCount = await outboxPublisherJob.PublishPendingMessagesAsync(cancellationToken);

        Assert.Equal(0, publishedMessageCount);
        var outboxMessage = await FindSingleOutboxMessageAsync(cancellationToken);
        Assert.Null(outboxMessage.ProcessedAt);
        Assert.Equal(1, outboxMessage.AttemptCount);
        Assert.False(string.IsNullOrWhiteSpace(outboxMessage.LastError));
    }

    [Fact]
    public async Task StartupWithWrongCredentials_FailsWithoutRetrying()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wrongCredentialsOverrides = new Dictionary<string, string?>
        {
            ["RabbitMq:Password"] = "wrong-password",
            ["RabbitMq:ConnectionAttempts"] = "5",
        };
        await using var wrongCredentialsEnvironment = fixture.CreateEnvironment(wrongCredentialsOverrides);
        var topologyInitializer = wrongCredentialsEnvironment.Services.GetServices<IHostedService>().OfType<RabbitMqTopologyInitializer>().Single();
        var stopwatch = Stopwatch.StartNew();

        var startupException = await Assert.ThrowsAnyAsync<Exception>(() => topologyInitializer.StartAsync(cancellationToken));

        stopwatch.Stop();
        Assert.True(RabbitMqFailures.IsConfigurationError(startupException));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    private static async Task<SampleAggregate> SaveSampleAsync(
        FinanceTestEnvironment targetEnvironment,
        string name,
        CancellationToken cancellationToken)
    {
        var sampleAggregate = SampleAggregate.Create(name, targetEnvironment.TimeProvider.GetUtcNow());
        await using var scope = targetEnvironment.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        dbContext.Set<SampleAggregate>().Add(sampleAggregate);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return sampleAggregate;
    }

    private static async Task<List<OutboxMessage>> FindOutboxMessagesAsync(
        FinanceTestEnvironment targetEnvironment,
        CancellationToken cancellationToken)
    {
        await using var scope = targetEnvironment.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        return await dbContext.Set<OutboxMessage>().AsNoTracking().ToListAsync(cancellationToken);
    }

    private Task<SampleAggregate> SaveSampleAsync(string name, CancellationToken cancellationToken)
    {
        return SaveSampleAsync(environment, name, cancellationToken);
    }

    private async Task<OutboxMessage> FindSingleOutboxMessageAsync(CancellationToken cancellationToken)
    {
        var outboxMessages = await FindOutboxMessagesAsync(environment, cancellationToken);
        return Assert.Single(outboxMessages);
    }

    private OutboxPublisherJob CreateOutboxPublisherJob(IMessagePublisher messagePublisher)
    {
        return new OutboxPublisherJob(
            environment.Services.GetRequiredService<IServiceScopeFactory>(),
            messagePublisher,
            environment.Services.GetRequiredService<IOptions<OutboxOptions>>(),
            environment.TimeProvider,
            environment.Services.GetRequiredService<ILogger<OutboxPublisherJob>>());
    }

    private static Task<IReadOnlyCollection<SampleMessage>> PublishUntilDeliveredAsync(
        OutboxPublisherJob outboxPublisherJob,
        ReceivedSampleMessages receivedSampleMessages,
        Guid sampleId,
        CancellationToken cancellationToken)
    {
        return Eventually.WaitUntilAsync(
            async probeCancellationToken =>
            {
                await outboxPublisherJob.PublishPendingMessagesAsync(probeCancellationToken);
                return receivedSampleMessages.All;
            },
            messages => messages.Any(message => message.SampleId == sampleId),
            DeliveryTimeout,
            cancellationToken);
    }

    private async Task WaitUntilTheSampleQueueHasAConsumerAsync(CancellationToken cancellationToken)
    {
        var rabbitMqConnectionProvider = environment.Services.GetRequiredService<RabbitMqConnectionProvider>();
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await Eventually.WaitUntilAsync(
            probeCancellationToken => channel.ConsumerCountAsync(SampleQueueName, probeCancellationToken),
            consumerCount => consumerCount >= 1,
            DeliveryTimeout,
            cancellationToken);
    }

    private async Task DeleteSampleQueueAsync(CancellationToken cancellationToken)
    {
        var rabbitMqConnectionProvider = environment.Services.GetRequiredService<RabbitMqConnectionProvider>();
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.QueueDeleteAsync(SampleQueueName, ifUnused: false, ifEmpty: false, cancellationToken: cancellationToken);
    }

    private async Task DeclareSampleQueueWithoutDeadLetteringAsync(CancellationToken cancellationToken)
    {
        var rabbitMqConnectionProvider = environment.Services.GetRequiredService<RabbitMqConnectionProvider>();
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(SampleQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
    }

    private async Task DeclareSampleQueueAsync(CancellationToken cancellationToken)
    {
        var rabbitMqOptions = environment.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        var rabbitMqConnectionProvider = environment.Services.GetRequiredService<RabbitMqConnectionProvider>();
        var connection = await rabbitMqConnectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqTopology.DeclareExchangesAsync(channel, rabbitMqOptions, cancellationToken);
        await RabbitMqTopology.DeclareConsumerQueueAsync(channel, rabbitMqOptions, SampleQueueName, SampleBindingKey, cancellationToken);
    }
}
