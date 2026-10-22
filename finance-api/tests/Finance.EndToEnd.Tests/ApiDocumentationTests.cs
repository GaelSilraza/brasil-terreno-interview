using System.Net;
using Finance.Testing;

namespace Finance.EndToEnd.Tests;

public sealed class ApiDocumentationTests(EndToEndTestFixture fixture) : IAsyncLifetime
{
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
    public async Task HealthEndpoint_ReportsHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await fixture.HttpClient.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task OpenApiDocument_DescribesEveryEndpoint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await fixture.HttpClient.GetAsync("/openapi/v1.json", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("\"/api/transfers\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/api/transfers/scheduled\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/api/transfers/{transferId}/cancel\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/api/transfers/{transferId}\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/api/accounts/{accountId}\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/api/transfer-limits/{accountId}\"", document, StringComparison.Ordinal);
        Assert.Contains("Idempotency-Key", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SwaggerUi_IsServed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await fixture.HttpClient.GetAsync("/swagger/index.html", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
