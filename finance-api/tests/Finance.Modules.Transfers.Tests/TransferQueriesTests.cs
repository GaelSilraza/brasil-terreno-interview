using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.UseCases.ListAccountTransfers;
using Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Shared.Domain;
using Finance.Testing;
using static Finance.Modules.Transfers.Tests.Support.TransfersScenario;

namespace Finance.Modules.Transfers.Tests;

public sealed class TransferQueriesTests(TransfersTestFixture fixture) : IAsyncLifetime
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
    public async Task ListingAccountTransfers_ReturnsSentAndReceivedNewestFirstWithHolderNames()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var anaAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var brunoAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var carlaAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var sentTransfer = await CreateImmediateTransferAsync(environment, anaAccountId, brunoAccountId, 100m, null, cancellationToken);
        environment.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        var receivedTransfer = await CreateImmediateTransferAsync(environment, carlaAccountId, anaAccountId, 50m, null, cancellationToken);

        var pageResult = await ListAccountTransfersAsync(environment, new ListAccountTransfersQuery(anaAccountId, null, null, 20), cancellationToken);

        var page = pageResult.Success;
        Assert.Null(page.NextCursor);
        Assert.Null(page.PreviousCursor);
        Assert.Collection(
            page.Items,
            newestTransfer =>
            {
                Assert.Equal(receivedTransfer.Success.Transfer.Id, newestTransfer.Id);
                Assert.Equal("Carla Mendes", newestTransfer.SourceHolderName);
                Assert.Equal("Ana Souza", newestTransfer.DestinationHolderName);
            },
            oldestTransfer =>
            {
                Assert.Equal(sentTransfer.Success.Transfer.Id, oldestTransfer.Id);
                Assert.Equal("Ana Souza", oldestTransfer.SourceHolderName);
                Assert.Equal("Bruno Lima", oldestTransfer.DestinationHolderName);
            });
    }

    [Fact]
    public async Task ListingAccountTransfersAcrossPages_ReturnsEverySentAndReceivedTransferOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var anaAccountId = await CreateAccountAsync(environment, "Ana Souza", 5000m, 0m, "Active", cancellationToken);
        var brunoAccountId = await CreateAccountAsync(environment, "Bruno Lima", 5000m, 0m, "Active", cancellationToken);
        var carlaAccountId = await CreateAccountAsync(environment, "Carla Mendes", 5000m, 0m, "Active", cancellationToken);
        await SetTransferLimitsAsync(environment, new SetTransferLimitsCommand(anaAccountId, 100000m, 1000, 100000m, 1000), cancellationToken);
        await SetTransferLimitsAsync(environment, new SetTransferLimitsCommand(brunoAccountId, 100000m, 1000, 100000m, 1000), cancellationToken);
        var createdTransferIds = new List<Guid>();

        for (var transferNumber = 0; transferNumber < 23; transferNumber++)
        {
            var transferResult = transferNumber % 3 == 0
                ? await CreateImmediateTransferAsync(environment, brunoAccountId, anaAccountId, 10m, null, cancellationToken)
                : await CreateImmediateTransferAsync(environment, anaAccountId, transferNumber % 2 == 0 ? brunoAccountId : carlaAccountId, 10m, null, cancellationToken);
            createdTransferIds.Add(transferResult.Success.Transfer.Id);
            environment.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        }

        var listedTransfers = new List<TransferResponse>();
        string? cursor = null;

        do
        {
            var pageResult = await ListAccountTransfersAsync(environment, new ListAccountTransfersQuery(anaAccountId, cursor, "next", 5), cancellationToken);
            var page = pageResult.Success;
            Assert.Equal(cursor is null, page.PreviousCursor is null);
            listedTransfers.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(23, listedTransfers.Select(transfer => transfer.Id).Distinct().Count());
        Assert.Equal(createdTransferIds.AsEnumerable().Reverse(), listedTransfers.Select(transfer => transfer.Id));
        Assert.Contains(listedTransfers, transfer => transfer.DestinationAccountId == anaAccountId);
        Assert.Contains(listedTransfers, transfer => transfer.SourceAccountId == anaAccountId);
    }

    [Fact]
    public async Task ListingAccountTransfersWithoutAccountId_ReturnsRequiredFieldError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var missingAccountIdResult = await ListAccountTransfersAsync(environment, new ListAccountTransfersQuery(null, null, null, null), cancellationToken);
        var emptyAccountIdResult = await ListAccountTransfersAsync(environment, new ListAccountTransfersQuery(Guid.Empty, null, null, null), cancellationToken);

        Assert.All(new[] { missingAccountIdResult, emptyAccountIdResult }, pageResult =>
        {
            Assert.True(pageResult.IsFailure);
            Assert.Equal(Error.ValidationFailedCode, pageResult.Failure.Code);
            var fieldError = Assert.Single(pageResult.Failure.FieldErrors);
            Assert.Equal("accountId", fieldError.Field);
            Assert.Equal(FieldErrorCodes.Required, fieldError.Code);
        });
    }

    [Fact]
    public async Task GettingTransferLimits_WithoutCustomPolicy_ReturnsDefaultsAndCurrentUsage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, null, cancellationToken);

        var limitsResult = await GetTransferLimitsAsync(environment, sourceAccountId, cancellationToken);

        Assert.True(limitsResult.IsSuccessful);
        var limits = limitsResult.Success;
        Assert.Equal(sourceAccountId, limits.AccountId);
        Assert.False(limits.IsCustom);
        Assert.Equal(TransferPeriod.Daytime, limits.CurrentPeriod);
        Assert.Equal(5000m, limits.Daytime.MaxAmountPerHour);
        Assert.Equal(5, limits.Daytime.MaxAttemptsPerHour);
        Assert.Equal(1000m, limits.Nighttime.MaxAmountPerHour);
        Assert.Equal(3, limits.Nighttime.MaxAttemptsPerHour);
        Assert.Equal(1, limits.Usage.AttemptsInLastHour);
        Assert.Equal(100m, limits.Usage.CompletedAmountInLastHour);
        Assert.Equal(4, limits.Usage.RemainingAttempts);
        Assert.Equal(4900m, limits.Usage.RemainingAmount);
    }

    [Fact]
    public async Task SettingTransferLimitsTwice_UpdatesTheSamePolicy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);

        var firstSet = await SetTransferLimitsAsync(environment, new SetTransferLimitsCommand(accountId, 8000m, 10, 2000m, 4), cancellationToken);
        var secondSet = await SetTransferLimitsAsync(environment, new SetTransferLimitsCommand(accountId, 6000m, 8, 1500m, 2), cancellationToken);
        var limitsResult = await GetTransferLimitsAsync(environment, accountId, cancellationToken);

        Assert.True(firstSet.IsSuccessful);
        Assert.True(secondSet.IsSuccessful);
        var limits = limitsResult.Success;
        Assert.True(limits.IsCustom);
        Assert.Equal(6000m, limits.Daytime.MaxAmountPerHour);
        Assert.Equal(8, limits.Daytime.MaxAttemptsPerHour);
        Assert.Equal(1500m, limits.Nighttime.MaxAmountPerHour);
        Assert.Equal(2, limits.Nighttime.MaxAttemptsPerHour);
        Assert.Equal(1, await CountTransferLimitPoliciesAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task SettingTransferLimits_ForUnknownAccount_ReturnsAccountNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var unknownAccountId = Guid.CreateVersion7(environment.TimeProvider.GetUtcNow());

        var setResult = await SetTransferLimitsAsync(environment, new SetTransferLimitsCommand(unknownAccountId, 8000m, 10, 2000m, 4), cancellationToken);

        Assert.True(setResult.IsFailure);
        Assert.Equal("AccountNotFound", setResult.Failure.Code);
        Assert.Equal(0, await CountTransferLimitPoliciesAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task GettingUnknownTransfer_ReturnsTransferNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var unknownTransferId = Guid.CreateVersion7(environment.TimeProvider.GetUtcNow());

        var transferResult = await GetTransferAsync(environment, unknownTransferId, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("TransferNotFound", transferResult.Failure.Code);
    }
}
