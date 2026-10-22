using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Finance.EndToEnd.Tests.Support;

public sealed class FinanceApiClient(HttpClient httpClient)
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public const string IdempotentReplayedHeader = "Idempotent-Replayed";

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static string CreateDocumentNumber()
    {
        return Random.Shared.NextInt64(10000000000, 99999999999).ToString(CultureInfo.InvariantCulture);
    }

    public static async Task<TPayload> ReadAsync<TPayload>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<TPayload>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException($"The response body could not be read as {typeof(TPayload).Name}.");
    }

    public Task<HttpResponseMessage> CreateAccountAsync(
        string fullName,
        string documentNumber,
        decimal initialBalance,
        decimal overdraftLimit,
        CancellationToken cancellationToken)
    {
        var request = new { fullName, documentNumber, initialBalance, overdraftLimit };
        return httpClient.PostAsJsonAsync("/api/accounts", request, JsonOptions, cancellationToken);
    }

    public async Task<AccountPayload> OpenAccountAsync(
        string fullName,
        decimal initialBalance,
        decimal overdraftLimit,
        CancellationToken cancellationToken)
    {
        using var response = await CreateAccountAsync(fullName, CreateDocumentNumber(), initialBalance, overdraftLimit, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<AccountPayload>(response, cancellationToken);
    }

    public Task<HttpResponseMessage> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return httpClient.GetAsync($"/api/accounts/{accountId}", cancellationToken);
    }

    public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        using var response = await GetAccountAsync(accountId, cancellationToken);
        response.EnsureSuccessStatusCode();
        var account = await ReadAsync<AccountPayload>(response, cancellationToken);
        return account.Balance;
    }

    public Task<HttpResponseMessage> GetAccountsAsync(
        string? search,
        string? cursor,
        string? direction,
        int? limit,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildRequestUri("/api/accounts", ("search", search), ("cursor", cursor), ("direction", direction), ("limit", FormatLimit(limit)));
        return httpClient.GetAsync(requestUri, cancellationToken);
    }

    public async Task<CursorPagePayload<AccountPayload>> ListAccountsAsync(
        string? search,
        string? cursor,
        string? direction,
        int? limit,
        CancellationToken cancellationToken)
    {
        using var response = await GetAccountsAsync(search, cursor, direction, limit, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<CursorPagePayload<AccountPayload>>(response, cancellationToken);
    }

    public Task<HttpResponseMessage> GetEntriesAsync(
        Guid accountId,
        string? cursor,
        string? direction,
        int? limit,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildRequestUri($"/api/accounts/{accountId}/entries", ("cursor", cursor), ("direction", direction), ("limit", FormatLimit(limit)));
        return httpClient.GetAsync(requestUri, cancellationToken);
    }

    public async Task<CursorPagePayload<AccountEntryPayload>> ListEntriesAsync(
        Guid accountId,
        string? cursor,
        string? direction,
        int? limit,
        CancellationToken cancellationToken)
    {
        using var response = await GetEntriesAsync(accountId, cursor, direction, limit, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<CursorPagePayload<AccountEntryPayload>>(response, cancellationToken);
    }

    public Task<HttpResponseMessage> UpdateOverdraftLimitAsync(Guid accountId, decimal overdraftLimit, CancellationToken cancellationToken)
    {
        var request = new { overdraftLimit };
        return httpClient.PutAsJsonAsync($"/api/accounts/{accountId}/overdraft-limit", request, JsonOptions, cancellationToken);
    }

    public Task<HttpResponseMessage> UpdateAccountStatusAsync(Guid accountId, string status, CancellationToken cancellationToken)
    {
        var request = new { status };
        return httpClient.PutAsJsonAsync($"/api/accounts/{accountId}/status", request, JsonOptions, cancellationToken);
    }

    public Task<HttpResponseMessage> PostTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var request = new { sourceAccountId, destinationAccountId, amount };
        return SendWithIdempotencyKeyAsync("/api/transfers", request, idempotencyKey, cancellationToken);
    }

    public Task<HttpResponseMessage> PostScheduledTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        DateTimeOffset scheduledFor,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var request = new { sourceAccountId, destinationAccountId, amount, scheduledFor };
        return SendWithIdempotencyKeyAsync("/api/transfers/scheduled", request, idempotencyKey, cancellationToken);
    }

    public Task<HttpResponseMessage> GetTransferAsync(Guid transferId, CancellationToken cancellationToken)
    {
        return httpClient.GetAsync($"/api/transfers/{transferId}", cancellationToken);
    }

    public async Task<TransferPayload> ReadTransferAsync(Guid transferId, CancellationToken cancellationToken)
    {
        using var response = await GetTransferAsync(transferId, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<TransferPayload>(response, cancellationToken);
    }

    public Task<HttpResponseMessage> CancelTransferAsync(Guid transferId, CancellationToken cancellationToken)
    {
        return httpClient.PostAsync($"/api/transfers/{transferId}/cancel", content: null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetTransfersAsync(
        Guid? accountId,
        string? cursor,
        string? direction,
        int? limit,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildRequestUri(
            "/api/transfers",
            ("accountId", accountId?.ToString()),
            ("cursor", cursor),
            ("direction", direction),
            ("limit", FormatLimit(limit)));
        return httpClient.GetAsync(requestUri, cancellationToken);
    }

    public async Task<CursorPagePayload<TransferPayload>> ListTransfersAsync(
        Guid accountId,
        string? cursor,
        string? direction,
        int? limit,
        CancellationToken cancellationToken)
    {
        using var response = await GetTransfersAsync(accountId, cursor, direction, limit, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<CursorPagePayload<TransferPayload>>(response, cancellationToken);
    }

    public Task<HttpResponseMessage> GetTransferLimitsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return httpClient.GetAsync($"/api/transfer-limits/{accountId}", cancellationToken);
    }

    public async Task<TransferLimitsPayload> ReadTransferLimitsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        using var response = await GetTransferLimitsAsync(accountId, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<TransferLimitsPayload>(response, cancellationToken);
    }

    public Task<HttpResponseMessage> SetTransferLimitsAsync(
        Guid accountId,
        decimal daytimeMaxAmountPerHour,
        int daytimeMaxAttemptsPerHour,
        decimal nighttimeMaxAmountPerHour,
        int nighttimeMaxAttemptsPerHour,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            daytimeMaxAmountPerHour,
            daytimeMaxAttemptsPerHour,
            nighttimeMaxAmountPerHour,
            nighttimeMaxAttemptsPerHour,
        };
        return httpClient.PutAsJsonAsync($"/api/transfer-limits/{accountId}", request, JsonOptions, cancellationToken);
    }

    public async Task AllowManyTransfersPerHourAsync(Guid accountId, CancellationToken cancellationToken)
    {
        using var response = await SetTransferLimitsAsync(accountId, 100000m, 100, 100000m, 100, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<HttpResponseMessage> SendRawJsonAsync(HttpMethod method, string requestUri, string json, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(method, requestUri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        return await httpClient.SendAsync(requestMessage, cancellationToken);
    }

    private static string? FormatLimit(int? limit)
    {
        return limit?.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildRequestUri(string path, params (string Name, string? Value)[] queryParameters)
    {
        var queryParts = queryParameters
            .Where(queryParameter => queryParameter.Value is not null)
            .Select(queryParameter => $"{queryParameter.Name}={Uri.EscapeDataString(queryParameter.Value ?? string.Empty)}")
            .ToList();
        return queryParts.Count == 0 ? path : $"{path}?{string.Join('&', queryParts)}";
    }

    private async Task<HttpResponseMessage> SendWithIdempotencyKeyAsync<TRequest>(
        string requestUri,
        TRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        if (idempotencyKey is not null)
        {
            requestMessage.Headers.Add(IdempotencyKeyHeader, idempotencyKey);
        }

        return await httpClient.SendAsync(requestMessage, cancellationToken);
    }
}
