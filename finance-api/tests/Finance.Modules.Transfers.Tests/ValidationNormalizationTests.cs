using Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;
using Finance.Shared.Domain;
using Finance.Testing;
using static Finance.Modules.Transfers.Tests.Support.TransfersScenario;

namespace Finance.Modules.Transfers.Tests;

public sealed class ValidationNormalizationTests(TransfersTestFixture fixture) : IAsyncLifetime
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
    public async Task TransferWithEveryFieldInvalid_ReturnsOneNormalizedErrorPerRule()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var transferResult = await CreateImmediateTransferAsync(environment, Guid.Empty, Guid.Empty, -1m, "   ", cancellationToken);

        Assert.True(transferResult.IsFailure);
        Assert.Equal(Error.ValidationFailedCode, transferResult.Failure.Code);
        var fieldErrors = transferResult.Failure.FieldErrors;
        Assert.Contains(fieldErrors, fieldError => fieldError.Field == "sourceAccountId" && fieldError.Code == FieldErrorCodes.Required);
        Assert.Contains(fieldErrors, fieldError => fieldError.Field == "destinationAccountId" && fieldError.Code == FieldErrorCodes.Required);
        Assert.Contains(fieldErrors, fieldError => fieldError.Field == "amount" && fieldError.Code == FieldErrorCodes.OutOfRange);
        Assert.Contains(fieldErrors, fieldError => fieldError.Field == "idempotencyKey" && fieldError.Code == FieldErrorCodes.InvalidFormat);
        Assert.All(fieldErrors, fieldError => Assert.Contains(fieldError.Code, FieldErrorCodes.All));
        Assert.Equal(0, await CountTransfersAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task SettingLimitsOutOfRange_ReturnsOutOfRangeWithBounds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var command = new SetTransferLimitsCommand(accountId, 1000m, 0, 500m, 3);

        var setLimitsResult = await SetTransferLimitsAsync(environment, command, cancellationToken);

        Assert.True(setLimitsResult.IsFailure);
        Assert.Equal(Error.ValidationFailedCode, setLimitsResult.Failure.Code);
        var fieldError = Assert.Single(setLimitsResult.Failure.FieldErrors);
        Assert.Equal("daytimeMaxAttemptsPerHour", fieldError.Field);
        Assert.Equal(FieldErrorCodes.OutOfRange, fieldError.Code);
        Assert.Equal(1, Assert.IsType<int>(fieldError.Params["from"]));
        Assert.Equal(1000, Assert.IsType<int>(fieldError.Params["to"]));
        Assert.Equal(0, await CountTransferLimitPoliciesAsync(environment, cancellationToken));
    }

    [Fact]
    public async Task SchedulingWithTooManyDecimals_ReturnsInvalidPrecision()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccountId = await CreateAccountAsync(environment, "Ana Souza", 500m, 1000m, "Active", cancellationToken);
        var destinationAccountId = await CreateAccountAsync(environment, "Bruno Lima", 0m, 0m, "Active", cancellationToken);
        var scheduledFor = environment.TimeProvider.GetUtcNow().AddDays(1);

        var scheduleResult = await ScheduleTransferAsync(environment, sourceAccountId, destinationAccountId, 1.001m, scheduledFor, null, cancellationToken);

        Assert.True(scheduleResult.IsFailure);
        Assert.Equal(Error.ValidationFailedCode, scheduleResult.Failure.Code);
        var fieldError = Assert.Single(scheduleResult.Failure.FieldErrors);
        Assert.Equal("amount", fieldError.Field);
        Assert.Equal(FieldErrorCodes.InvalidPrecision, fieldError.Code);
        Assert.Equal(0, await CountTransfersAsync(environment, cancellationToken));
    }
}
