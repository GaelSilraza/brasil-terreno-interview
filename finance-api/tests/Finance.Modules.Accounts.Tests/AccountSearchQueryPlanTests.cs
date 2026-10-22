using Finance.Modules.Accounts.Application.UseCases.ListAccounts;
using Finance.Modules.Accounts.Infrastructure;
using Finance.Modules.Accounts.Tests.Support;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Finance.Modules.Accounts.Tests;

public sealed class AccountSearchQueryPlanTests(AccountsTestFixture fixture) : IAsyncLifetime
{
    private const int SeededPersonCount = 20000;

    private readonly FinanceTestEnvironment environment = new(
        fixture.ConnectionString,
        fixture.CreateConfigurationValues(),
        (services, configuration) =>
        {
            services.AddAccountsModule(configuration);
            services.AddSingleton<QueryPlanCapturingInterceptor>();
            services.ConfigureDbContext<FinanceDbContext>((serviceProvider, options) =>
                options.AddInterceptors(serviceProvider.GetRequiredService<QueryPlanCapturingInterceptor>()));
        });

    private QueryPlanCapturingInterceptor Interceptor => environment.Services.GetRequiredService<QueryPlanCapturingInterceptor>();

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await environment.ResetDatabaseAsync(cancellationToken);
        await SeedPersonsAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await environment.DisposeAsync();
    }

    [Fact]
    public async Task ListingWithoutSearch_UsesTheNameAndIdIndex()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery(null, null, null, null), cancellationToken);

        Assert.Contains("ix_persons_full_name_id", SingleCapturedPlan(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchingByRareNameFragment_UsesTheTrigramIndex()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery("Rarissima", null, null, null), cancellationToken);

        Assert.Contains("ix_persons_full_name_trgm", SingleCapturedPlan(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchingByDocumentPrefix_UsesThePatternOpsIndex()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery("0000001234", null, null, null), cancellationToken);

        Assert.Contains("ux_persons_document_number", SingleCapturedPlan(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FollowingACursor_UsesTheNameAndIdIndex()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstPageResult = await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery(null, null, null, null), cancellationToken);
        Interceptor.Plans.Clear();

        await AccountsScenario.ListAccountsAsync(environment, new ListAccountsQuery(null, firstPageResult.Success.NextCursor, "next", null), cancellationToken);

        Assert.Contains("ix_persons_full_name_id", SingleCapturedPlan(), StringComparison.Ordinal);
    }

    private string SingleCapturedPlan()
    {
        var plan = Assert.Single(Interceptor.Plans);
        TestContext.Current.SendDiagnosticMessage(plan);
        Assert.DoesNotContain("Seq Scan on persons", plan, StringComparison.Ordinal);
        return plan;
    }

    private async Task SeedPersonsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var seedCommand = new NpgsqlCommand(
            """
            WITH inserted_persons AS (
                INSERT INTO persons (id, full_name, document_number, created_at, updated_at)
                SELECT gen_random_uuid(), 'Pessoa ' || lpad(number::text, 5, '0'), lpad(number::text, 11, '0'), @now, @now
                FROM generate_series(1, @personCount) AS number
                UNION ALL
                SELECT gen_random_uuid(), 'Zuleica Rarissima', '99999999999', @now, @now
                RETURNING id
            )
            INSERT INTO accounts (id, person_id, overdraft_limit, status, created_at, updated_at)
            SELECT gen_random_uuid(), inserted_persons.id, 0, 'Active', @now, @now
            FROM inserted_persons
            """,
            connection))
        {
            seedCommand.Parameters.AddWithValue("now", environment.TimeProvider.GetUtcNow());
            seedCommand.Parameters.AddWithValue("personCount", SeededPersonCount);
            await seedCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var analyzeCommand = new NpgsqlCommand("ANALYZE persons, accounts", connection);
        await analyzeCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
