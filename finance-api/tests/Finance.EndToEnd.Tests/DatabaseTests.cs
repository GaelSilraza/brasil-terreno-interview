using Finance.EndToEnd.Tests.Support;
using Finance.Migrator;
using Finance.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Finance.EndToEnd.Tests;

public sealed class DatabaseTests(EndToEndTestFixture fixture) : IAsyncLifetime
{
    private static readonly string[] ExpectedIndexNames =
    [
        "pk_persons",
        "pk_accounts",
        "pk_account_entries",
        "pk_transfers",
        "pk_transfer_limit_policies",
        "pk_outbox_messages",
        "ux_persons_document_number",
        "ix_persons_full_name_id",
        "ix_persons_full_name_trgm",
        "ux_accounts_person_id",
        "ix_account_entries_account_id_id",
        "ix_account_entries_counterpart_account_id",
        "ux_account_entries_transfer_id_entry_type",
        "ux_account_entries_opening",
        "ix_transfers_source_account_id_attempted_at",
        "ix_transfers_source_account_id_id",
        "ix_transfers_destination_account_id_id",
        "ix_transfers_scheduled_for_due",
        "ix_transfers_processing_started_at_stale",
        "ux_transfers_idempotency_key",
        "ix_outbox_messages_unprocessed",
    ];

    private static readonly string[] ExpectedConstraintNames =
    [
        "pk_persons",
        "pk_accounts",
        "pk_account_entries",
        "pk_transfers",
        "pk_transfer_limit_policies",
        "pk_outbox_messages",
        "fk_accounts_persons_person_id",
        "fk_account_entries_accounts_account_id",
        "fk_account_entries_accounts_counterpart_account_id",
        "fk_transfers_accounts_source_account_id",
        "fk_transfers_accounts_destination_account_id",
        "fk_transfer_limit_policies_accounts_account_id",
        "ck_accounts_overdraft_limit_non_negative",
        "ck_transfers_amount_positive",
        "ck_transfers_source_differs_from_destination",
    ];

    private static readonly string[] FilteredIndexNames =
    [
        "ix_transfers_scheduled_for_due",
        "ix_transfers_processing_started_at_stale",
        "ux_transfers_idempotency_key",
        "ix_outbox_messages_unprocessed",
        "ux_account_entries_opening",
    ];

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
    public async Task Migrations_CreateNamedPrimaryKeysForeignKeysAndIndexes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var indexDefinitions = await QueryPairsAsync(
            "SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'public'",
            cancellationToken);
        var constraintNames = await QueryPairsAsync(
            "SELECT constraint_name, constraint_type FROM information_schema.table_constraints WHERE constraint_schema = 'public'",
            cancellationToken);
        var schemaNames = await QueryPairsAsync(
            "SELECT schema_name, schema_name FROM information_schema.schemata WHERE schema_name IN ('accounts', 'transfers', 'messaging')",
            cancellationToken);

        Assert.Empty(schemaNames);
        Assert.All(ExpectedIndexNames, indexName => Assert.Contains(indexName, indexDefinitions.Keys));
        Assert.All(ExpectedConstraintNames, constraintName => Assert.Contains(constraintName, constraintNames.Keys));
        Assert.All(FilteredIndexNames, indexName => Assert.Contains(" WHERE ", indexDefinitions[indexName], StringComparison.Ordinal));
        Assert.DoesNotContain("ix_transfers_destination_account_id", indexDefinitions.Keys);
        Assert.Contains("varchar_pattern_ops", indexDefinitions["ux_persons_document_number"], StringComparison.Ordinal);
        Assert.Contains("USING gin", indexDefinitions["ix_persons_full_name_trgm"], StringComparison.Ordinal);
        Assert.Contains("gin_trgm_ops", indexDefinitions["ix_persons_full_name_trgm"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrations_DeriveBalancesFromTheLedgerInsteadOfStoringThem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var viewNames = await QueryPairsAsync("SELECT viewname, viewname FROM pg_views WHERE schemaname = 'public'", cancellationToken);
        var accountColumns = await QueryPairsAsync(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'accounts'",
            cancellationToken);
        var indexDefinitions = await QueryPairsAsync("SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'public'", cancellationToken);

        Assert.Contains("account_balances", viewNames.Keys);
        Assert.DoesNotContain("balance", accountColumns.Keys);
        Assert.Contains("INCLUDE (entry_type, amount)", indexDefinitions["ix_account_entries_account_id_id"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrations_EnableTheTrigramExtension()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var extensionNames = await QueryPairsAsync("SELECT extname, extname FROM pg_extension", cancellationToken);

        Assert.Contains("pg_trgm", extensionNames.Keys);
    }

    [Fact]
    public async Task ForeignKeyColumns_AreAllIndexed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var unindexedForeignKeys = await QueryPairsAsync(
            "SELECT c.conname, c.conname FROM pg_constraint c WHERE c.contype = 'f' AND NOT EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = c.conrelid AND i.indkey[0] = c.conkey[1])",
            cancellationToken);

        Assert.Empty(unindexedForeignKeys);
    }

    [Fact]
    public async Task MigratingAnEmptyDatabase_AppliesEveryMigrationWithoutLoggingErrors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databaseName = $"finance_empty_{Guid.CreateVersion7():N}";
        await ExecuteOnServerAsync($"CREATE DATABASE {databaseName}", cancellationToken);
        var recordingLoggerProvider = new RecordingLoggerProvider();

        try
        {
            var emptyDatabaseConnectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = databaseName }.ConnectionString;
            var configurationValues = fixture.CreateConfigurationValues();
            configurationValues["ConnectionStrings:Finance"] = emptyDatabaseConnectionString;
            configurationValues["Seed:Enabled"] = "true";
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(recordingLoggerProvider));
            services.AddSingleton<IConfiguration>(configuration);
            DatabaseInitializer.Register(services, configuration);
            await using (var serviceProvider = services.BuildServiceProvider())
            {
                await serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
            }

            var publicTableCount = await CountAsync(
                emptyDatabaseConnectionString,
                "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__ef_migrations_history'",
                cancellationToken);
            var appliedMigrationCount = await CountAsync(emptyDatabaseConnectionString, "SELECT count(*) FROM __ef_migrations_history", cancellationToken);
            var seededAccountCount = await CountAsync(emptyDatabaseConnectionString, "SELECT count(*) FROM accounts", cancellationToken);

            Assert.DoesNotContain(recordingLoggerProvider.Entries, entry => entry.Level >= LogLevel.Warning);
            Assert.Equal(6, publicTableCount);
            Assert.Equal(5, appliedMigrationCount);
            Assert.Equal(5, seededAccountCount);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", cancellationToken);
        }
    }

    [Fact]
    public async Task Seed_CreatesTheFiveSeedAccountsOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await RunDatabaseInitializerWithSeedAsync(cancellationToken);
        await RunDatabaseInitializerWithSeedAsync(cancellationToken);

        var accounts = await ApiClient.ListAccountsAsync(null, null, null, 100, cancellationToken);
        Assert.Equal(5, accounts.Items.Count);
        Assert.Null(accounts.NextCursor);
        Assert.Null(accounts.PreviousCursor);
        Assert.Equal(
            ["Ana Souza", "Bruno Lima", "Carla Mendes", "Daniel Rocha", "Elisa Martins"],
            accounts.Items.Select(account => account.HolderName).ToArray());
        Assert.Equal(
            ["10000000001", "10000000002", "10000000003", "10000000004", "10000000005"],
            accounts.Items.Select(account => account.DocumentNumber).ToArray());
        Assert.Equal([500m, -800m, 10000m, 300m, 2000m], accounts.Items.Select(account => account.Balance).ToArray());
        Assert.Equal([1000m, 1000m, 0m, 0m, 500m], accounts.Items.Select(account => account.OverdraftLimit).ToArray());
        Assert.Equal(["Active", "Active", "Active", "Blocked", "Inactive"], accounts.Items.Select(account => account.Status).ToArray());
    }

    private async Task RunDatabaseInitializerWithSeedAsync(CancellationToken cancellationToken)
    {
        var configurationValues = fixture.CreateConfigurationValues();
        configurationValues["Seed:Enabled"] = "true";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        DatabaseInitializer.Register(services, configuration);
        await using var serviceProvider = services.BuildServiceProvider();
        await serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
    }

    private async Task ExecuteOnServerAsync(string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> CountAsync(string connectionString, string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(commandText, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<Dictionary<string, string>> QueryPairsAsync(string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(commandText, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);

        while (await reader.ReadAsync(cancellationToken))
        {
            pairs[reader.GetString(0)] = reader.GetString(1);
        }

        return pairs;
    }
}
