using System.Text.Json;
using Finance.Modules.Transfers.Application.Messages;
using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Domain.Enums;
using Finance.Testing;
using static Finance.Modules.Transfers.Tests.Support.TransfersScenario;

namespace Finance.Modules.Transfers.Tests;

public sealed class ImmediateTransferTests(TransfersTestFixture fixture) : IAsyncLifetime
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
    public async Task TransferWithSufficientBalance_CompletesAndMovesFunds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, null, cancellationToken);

        Assert.True(transferResult.IsSuccessful);
        var transfer = transferResult.Success.Transfer;
        Assert.Equal(TransferStatus.Completed, transfer.Status);
        Assert.Equal(TransferType.Immediate, transfer.Type);
        Assert.Equal("Ana Souza", transfer.SourceHolderName);
        Assert.Equal("Bruno Lima", transfer.DestinationHolderName);
        Assert.NotNull(transfer.AttemptedAt);
        Assert.NotNull(transfer.CompletedAt);
        Assert.False(transferResult.Success.IsIdempotentReplay);
        Assert.Equal(400m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(100m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task TransferUsingOverdraft_CompletesLeavingNegativeBalance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 1000m, null, cancellationToken);

        Assert.True(transferResult.IsSuccessful);
        Assert.Equal(TransferStatus.Completed, transferResult.Success.Transfer.Status);
        Assert.Equal(-500m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(1000m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task TransferAboveBalancePlusOverdraft_FailsWithInsufficientFundsAndIsPersistedAsFailed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 1500.01m, null, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("InsufficientFunds", transferResult.Failure.Code);
        var transferId = Assert.IsType<Guid>(transferResult.Failure.Metadata[TransferMetadataKeys.TransferId]);
        var persistedTransfer = await GetTransferAsync(environment, transferId, cancellationToken);
        Assert.True(persistedTransfer.IsSuccessful);
        Assert.Equal(TransferStatus.Failed, persistedTransfer.Success.Status);
        Assert.Equal("InsufficientFunds", persistedTransfer.Success.FailureCode);
        Assert.NotNull(persistedTransfer.Success.FailedAt);
        Assert.Equal(500m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(0m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task TransferToAccountUsingOverdraft_CoversOverdraftFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Carla Mendes", 10000m, 0m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", -800m, 1000m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 1000m, null, cancellationToken);

        Assert.True(transferResult.IsSuccessful);
        Assert.Equal(TransferStatus.Completed, transferResult.Success.Transfer.Status);
        Assert.Equal(200m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
        Assert.Equal(9000m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
    }

    [Fact]
    public async Task TransferFromBlockedAccount_FailsWithSourceAccountInactive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Daniel Rocha", 300m, 0m, "Blocked", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, null, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("SourceAccountInactive", transferResult.Failure.Code);
        var transferId = Assert.IsType<Guid>(transferResult.Failure.Metadata[TransferMetadataKeys.TransferId]);
        var persistedTransfer = await GetTransferAsync(environment, transferId, cancellationToken);
        Assert.Equal(TransferStatus.Failed, persistedTransfer.Success.Status);
        Assert.Equal("SourceAccountInactive", persistedTransfer.Success.FailureCode);
        Assert.Equal(300m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
    }

    [Fact]
    public async Task TransferToInactiveAccount_FailsWithDestinationAccountInactive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Elisa Martins", 2000m, 500m, "Inactive", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, null, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("DestinationAccountInactive", transferResult.Failure.Code);
        var transferId = Assert.IsType<Guid>(transferResult.Failure.Metadata[TransferMetadataKeys.TransferId]);
        var persistedTransfer = await GetTransferAsync(environment, transferId, cancellationToken);
        Assert.Equal(TransferStatus.Failed, persistedTransfer.Success.Status);
        Assert.Equal("DestinationAccountInactive", persistedTransfer.Success.FailureCode);
        Assert.Equal(500m, await GetBalanceAsync(environment, sourceAccountId, cancellationToken));
        Assert.Equal(2000m, await GetBalanceAsync(environment, destinationAccountId, cancellationToken));
    }

    [Fact]
    public async Task TransferWithUnknownAccount_ReturnsNotFoundAndPersistsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var unknownAccountId = Guid.CreateVersion7(environment.TimeProvider.GetUtcNow());

        var transferResult = await CreateImmediateTransferAsync(environment, unknownAccountId, destinationAccountId, 100m, null, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("SourceAccountNotFound", transferResult.Failure.Code);
        Assert.Equal(0, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task TransferToSameAccount_ReturnsValidationFailedAndPersistsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, accountId, accountId, 100m, null, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("ValidationFailed", transferResult.Failure.Code);
        Assert.True(transferResult.Failure.ValidationErrors.ContainsKey("destinationAccountId"));
        Assert.Contains(transferResult.Failure.FieldErrors, fieldError => fieldError.Field == "destinationAccountId" && fieldError.Code == "MustBeDifferent");
        Assert.Equal(0, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task TransferWithZeroAmount_ReturnsValidationFailedAndPersistsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 0m, null, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal("ValidationFailed", transferResult.Failure.Code);
        Assert.True(transferResult.Failure.ValidationErrors.ContainsKey("amount"));
        Assert.Contains(transferResult.Failure.FieldErrors, fieldError => fieldError.Field == "amount" && fieldError.Code == "OutOfRange");
        Assert.Equal(0, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task CompletedTransfer_WritesCompletedEventToOutbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 100m, null, cancellationToken);

        Assert.True(transferResult.IsSuccessful);
        Assert.Equal(1, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.Completed, cancellationToken));
        Assert.Equal(0, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.Failed, cancellationToken));
    }

    [Fact]
    public async Task FailedTransfer_WritesFailedEventToOutbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);

        var transferResult = await CreateImmediateTransferAsync(environment, sourceAccountId, destinationAccountId, 2000m, null, cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal(1, await CountOutboxMessagesAsync(environment, TransferRoutingKeys.Failed, cancellationToken));
        var content = Assert.Single(await GetOutboxMessageContentsAsync(environment, TransferRoutingKeys.Failed, cancellationToken));
        using var contentDocument = JsonDocument.Parse(content);
        Assert.Equal("InsufficientFunds", contentDocument.RootElement.GetProperty("failureCode").GetString());
        Assert.Equal("Failed", contentDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("TransferFailed", contentDocument.RootElement.GetProperty("eventName").GetString());
    }
}
