using Finance.Shared.Application.Persistence;
using Finance.Shared.Infrastructure.Outbox;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Shared.Infrastructure.Tests.SampleModule;
using Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Shared.Infrastructure.Tests;

public sealed class OutboxTests(SharedInfrastructureTestFixture fixture) : IAsyncLifetime
{
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
    public async Task SavingAggregateWithDomainEvent_WritesOutboxMessageInTheSameTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sampleAggregate = SampleAggregate.Create("committed sample", environment.TimeProvider.GetUtcNow());

        await using (var scope = environment.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            dbContext.Set<SampleAggregate>().Add(sampleAggregate);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await using var assertionScope = environment.CreateScope();
        var assertionDbContext = assertionScope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var sampleCount = await assertionDbContext.Set<SampleAggregate>().CountAsync(cancellationToken);
        var outboxMessages = await assertionDbContext.Set<OutboxMessage>().AsNoTracking().ToListAsync(cancellationToken);
        Assert.Equal(1, sampleCount);
        var outboxMessage = Assert.Single(outboxMessages);
        Assert.Equal("testing.sample-created", outboxMessage.RoutingKey);
        Assert.Equal(nameof(SampleMessage), outboxMessage.Type);
        Assert.Null(outboxMessage.ProcessedAt);
        Assert.Contains(sampleAggregate.Id.ToString(), outboxMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RollingBackTransaction_DiscardsAggregateAndOutboxMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sampleAggregate = SampleAggregate.Create("rolled back sample", environment.TimeProvider.GetUtcNow());

        await using (var scope = environment.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            dbContext.Set<SampleAggregate>().Add(sampleAggregate);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await using var assertionScope = environment.CreateScope();
        var assertionDbContext = assertionScope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var sampleCount = await assertionDbContext.Set<SampleAggregate>().CountAsync(cancellationToken);
        var outboxMessageCount = await assertionDbContext.Set<OutboxMessage>().CountAsync(cancellationToken);
        Assert.Equal(0, sampleCount);
        Assert.Equal(0, outboxMessageCount);
    }
}
