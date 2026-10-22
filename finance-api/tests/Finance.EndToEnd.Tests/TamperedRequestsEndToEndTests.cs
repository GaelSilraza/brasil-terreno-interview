using System.Net;
using System.Text;
using Finance.EndToEnd.Tests.Support;
using Finance.Testing;
using Microsoft.AspNetCore.Hosting;

namespace Finance.EndToEnd.Tests;

public sealed class TamperedRequestsEndToEndTests(EndToEndTestFixture fixture) : IAsyncLifetime
{
    private static readonly string[] TransferBodyFields = ["sourceAccountId", "destinationAccountId", "amount"];

    private FinanceApiClient ApiClient => new(fixture.HttpClient);

    public async ValueTask InitializeAsync()
    {
        await fixture.StartHostsAsync();
        await using var environment = fixture.CreateEnvironment();
        await environment.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        fixture.Clock.SetUtcNow(TestClock.DaytimeInSaoPaulo);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task MalformedJson_Returns400WithInvalidValueFieldError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wellFormedIdsBody = $$"""{"sourceAccountId": "{{Guid.CreateVersion7()}}", "destinationAccountId": "{{Guid.CreateVersion7()}}", "amount": "abc"}""";

        using var response = await ApiClient.SendRawJsonAsync(
            HttpMethod.Post,
            "/api/transfers",
            """{"sourceAccountId": "x", "destinationAccountId": "y", "amount": "abc"}""",
            cancellationToken);
        using var amountOnlyResponse = await ApiClient.SendRawJsonAsync(HttpMethod.Post, "/api/transfers", wellFormedIdsBody, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.FieldErrors);
        Assert.Contains(problem.FieldErrors, fieldError => TransferBodyFields.Contains(fieldError.Field) && fieldError.Code == "InvalidValue");
        Assert.Equal(HttpStatusCode.BadRequest, amountOnlyResponse.StatusCode);
        var amountOnlyProblem = await FinanceApiClient.ReadAsync<ProblemPayload>(amountOnlyResponse, cancellationToken);
        Assert.Equal("ValidationFailed", amountOnlyProblem.Code);
        Assert.NotNull(amountOnlyProblem.FieldErrors);
        Assert.Contains(amountOnlyProblem.FieldErrors, fieldError => fieldError.Field == "amount" && fieldError.Code == "InvalidValue");
    }

    [Fact]
    public async Task NegativeAndTooPreciseAmounts_Return400WithNormalizedCodes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        using var negativeResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, -5m, null, cancellationToken);
        using var tooPreciseResponse = await ApiClient.PostTransferAsync(sourceAccount.Id, destinationAccount.Id, 1.001m, null, cancellationToken);

        var negativeError = await ReadSingleFieldErrorAsync(negativeResponse, "amount", cancellationToken);
        Assert.Equal("OutOfRange", negativeError.Code);
        Assert.Equal("greaterThan", negativeError.Params["comparison"].GetString());
        Assert.Equal(0m, negativeError.Params["comparisonValue"].GetDecimal());
        var tooPreciseError = await ReadSingleFieldErrorAsync(tooPreciseResponse, "amount", cancellationToken);
        Assert.Equal("InvalidPrecision", tooPreciseError.Code);
        Assert.Equal(2, tooPreciseError.Params["expectedScale"].GetInt32());
        var transfers = await ApiClient.ListTransfersAsync(sourceAccount.Id, null, null, null, cancellationToken);
        Assert.Empty(transfers.Items);
        Assert.Equal(500m, await ApiClient.GetBalanceAsync(sourceAccount.Id, cancellationToken));
    }

    [Fact]
    public async Task ScheduleInThePast_Returns400MustBeInFuture()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sourceAccount = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);
        var destinationAccount = await ApiClient.OpenAccountAsync("Bruno Lima", 0m, 0m, cancellationToken);

        using var response = await ApiClient.PostScheduledTransferAsync(
            sourceAccount.Id,
            destinationAccount.Id,
            100m,
            fixture.Clock.GetUtcNow().AddMinutes(-1),
            null,
            cancellationToken);

        var fieldError = await ReadSingleFieldErrorAsync(response, "scheduledFor", cancellationToken);
        Assert.Equal("MustBeInFuture", fieldError.Code);
    }

    [Fact]
    public async Task TransferLimitsOutOfBounds_Return400WithBounds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var account = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);

        using var response = await ApiClient.SetTransferLimitsAsync(account.Id, 5000m, 5000, 1000m, 3, cancellationToken);

        var fieldError = await ReadSingleFieldErrorAsync(response, "daytimeMaxAttemptsPerHour", cancellationToken);
        Assert.Equal("OutOfRange", fieldError.Code);
        Assert.Equal(1, fieldError.Params["from"].GetInt32());
        Assert.Equal(1000, fieldError.Params["to"].GetInt32());
        var limits = await ApiClient.ReadTransferLimitsAsync(account.Id, cancellationToken);
        Assert.False(limits.IsCustom);
    }

    [Fact]
    public async Task UnknownEnumStatus_Returns400InvalidValue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var account = await ApiClient.OpenAccountAsync("Ana Souza", 500m, 0m, cancellationToken);

        using var response = await ApiClient.SendRawJsonAsync(HttpMethod.Put, $"/api/accounts/{account.Id}/status", """{"status":"Deleted"}""", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.FieldErrors);
        Assert.Contains(problem.FieldErrors, fieldError => fieldError.Field == "status" && fieldError.Code == "InvalidValue");
        using var accountResponse = await ApiClient.GetAccountAsync(account.Id, cancellationToken);
        var persistedAccount = await FinanceApiClient.ReadAsync<AccountPayload>(accountResponse, cancellationToken);
        Assert.Equal("Active", persistedAccount.Status);
    }

    [Fact]
    public async Task OversizedSearch_Returns400InvalidLength()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var oversizedSearch = new string('a', 101);

        using var response = await ApiClient.GetAccountsAsync(oversizedSearch, null, null, null, cancellationToken);

        var fieldError = await ReadSingleFieldErrorAsync(response, "search", cancellationToken);
        Assert.Equal("InvalidLength", fieldError.Code);
        Assert.Equal(100, fieldError.Params["maxLength"].GetInt32());
    }

    [Fact]
    public async Task UnknownRouteAndWrongMethod_ReturnProblemDetailsWithCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var malformedIdResponse = await fixture.HttpClient.GetAsync("/api/transfers/not-a-uuid", cancellationToken);
        using var unknownRouteResponse = await fixture.HttpClient.GetAsync("/api/unknown", cancellationToken);
        using var wrongMethodResponse = await fixture.HttpClient.DeleteAsync("/api/accounts", cancellationToken);

        await AssertFrameworkProblemAsync(malformedIdResponse, HttpStatusCode.NotFound, "NotFound", cancellationToken);
        await AssertFrameworkProblemAsync(unknownRouteResponse, HttpStatusCode.NotFound, "NotFound", cancellationToken);
        await AssertFrameworkProblemAsync(wrongMethodResponse, HttpStatusCode.MethodNotAllowed, "MethodNotAllowed", cancellationToken);
    }

    [Fact]
    public async Task BodyThatIsNotJson_Returns415WithCodeAndPersistsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var content = new StringContent("fullName=Ana Souza", Encoding.UTF8, "text/plain");

        using var response = await fixture.HttpClient.PostAsync("/api/accounts", content, cancellationToken);

        await AssertFrameworkProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType", cancellationToken);
        var accounts = await ApiClient.ListAccountsAsync(null, null, null, null, cancellationToken);
        Assert.Empty(accounts.Items);
    }

    [Fact]
    public async Task BodyAboveTheServerLimit_Returns413WithCodeAndPersistsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var apiFactory = new FinanceApiFactory(fixture.CreateConfigurationValues(), fixture.Clock);
        await using var kestrelApiFactory = apiFactory.WithWebHostBuilder(builder => builder.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024));
        kestrelApiFactory.UseKestrel();
        using var kestrelHttpClient = kestrelApiFactory.CreateClient();
        var oversizedFullName = new string('a', 2048);

        using var response = await new FinanceApiClient(kestrelHttpClient).CreateAccountAsync(oversizedFullName, FinanceApiClient.CreateDocumentNumber(), 0m, 0m, cancellationToken);

        await AssertFrameworkProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "RequestRejected", cancellationToken);
        var accounts = await ApiClient.ListAccountsAsync(null, null, null, null, cancellationToken);
        Assert.Empty(accounts.Items);
    }

    private static async Task AssertFrameworkProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal(expectedCode, problem.Code);
        Assert.Equal(expectedCode, problem.Title);
        Assert.Equal((int)expectedStatusCode, problem.Status);
    }

    private static async Task<FieldErrorPayload> ReadSingleFieldErrorAsync(
        HttpResponseMessage response,
        string expectedField,
        CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await FinanceApiClient.ReadAsync<ProblemPayload>(response, cancellationToken);
        Assert.Equal("ValidationFailed", problem.Code);
        Assert.NotNull(problem.FieldErrors);
        var fieldError = Assert.Single(problem.FieldErrors);
        Assert.Equal(expectedField, fieldError.Field);
        return fieldError;
    }
}
