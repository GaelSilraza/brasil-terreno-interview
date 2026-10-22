using System.Globalization;
using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.UseCases.CancelTransfer;
using Finance.Modules.Transfers.Application.UseCases.CreateImmediateTransfer;
using Finance.Modules.Transfers.Application.UseCases.DispatchDueScheduledTransfers;
using Finance.Modules.Transfers.Application.UseCases.ExecuteScheduledTransfer;
using Finance.Modules.Transfers.Application.UseCases.GetTransfer;
using Finance.Modules.Transfers.Application.UseCases.GetTransferLimits;
using Finance.Modules.Transfers.Application.UseCases.ListAccountTransfers;
using Finance.Modules.Transfers.Application.UseCases.ScheduleTransfer;
using Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Domain;
using Finance.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Finance.Modules.Transfers.Tests.Support;

public static class TransfersScenario
{
    private static long documentNumberSequence;

    public static async Task<Guid> CreateAccountAsync(
        FinanceTestEnvironment environment,
        string fullName,
        decimal balance,
        decimal overdraftLimit,
        string status,
        CancellationToken cancellationToken)
    {
        var now = environment.TimeProvider.GetUtcNow();
        var personId = Guid.CreateVersion7(now);
        var accountId = Guid.CreateVersion7(now);
        var documentNumber = (10000000000L + Interlocked.Increment(ref documentNumberSequence)).ToString(CultureInfo.InvariantCulture);

        await using var connection = await OpenConnectionAsync(environment, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insertPersonCommand = new NpgsqlCommand(
            "INSERT INTO persons (id, full_name, document_number, created_at, updated_at) VALUES (@id, @fullName, @documentNumber, @createdAt, @updatedAt)",
            connection,
            transaction))
        {
            insertPersonCommand.Parameters.AddWithValue("id", personId);
            insertPersonCommand.Parameters.AddWithValue("fullName", fullName);
            insertPersonCommand.Parameters.AddWithValue("documentNumber", documentNumber);
            insertPersonCommand.Parameters.AddWithValue("createdAt", now);
            insertPersonCommand.Parameters.AddWithValue("updatedAt", now);
            await insertPersonCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertAccountCommand = new NpgsqlCommand(
            "INSERT INTO accounts (id, person_id, overdraft_limit, status, created_at, updated_at) VALUES (@id, @personId, @overdraftLimit, @status, @createdAt, @updatedAt)",
            connection,
            transaction))
        {
            insertAccountCommand.Parameters.AddWithValue("id", accountId);
            insertAccountCommand.Parameters.AddWithValue("personId", personId);
            insertAccountCommand.Parameters.AddWithValue("overdraftLimit", overdraftLimit);
            insertAccountCommand.Parameters.AddWithValue("status", status);
            insertAccountCommand.Parameters.AddWithValue("createdAt", now);
            insertAccountCommand.Parameters.AddWithValue("updatedAt", now);
            await insertAccountCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (balance != 0)
        {
            await using var insertOpeningEntryCommand = new NpgsqlCommand(
                "INSERT INTO account_entries (id, account_id, entry_type, amount, balance_after, created_at) VALUES (@id, @accountId, @entryType, @amount, @balanceAfter, @createdAt)",
                connection,
                transaction);
            insertOpeningEntryCommand.Parameters.AddWithValue("id", Guid.CreateVersion7(now));
            insertOpeningEntryCommand.Parameters.AddWithValue("accountId", accountId);
            insertOpeningEntryCommand.Parameters.AddWithValue("entryType", balance < 0 ? "Debit" : "Credit");
            insertOpeningEntryCommand.Parameters.AddWithValue("amount", Math.Abs(balance));
            insertOpeningEntryCommand.Parameters.AddWithValue("balanceAfter", balance);
            insertOpeningEntryCommand.Parameters.AddWithValue("createdAt", now);
            await insertOpeningEntryCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return accountId;
    }

    public static async Task<decimal> GetBalanceAsync(FinanceTestEnvironment environment, Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(environment, cancellationToken);
        await using var command = new NpgsqlCommand("SELECT balance FROM account_balances WHERE account_id = @id", connection);
        command.Parameters.AddWithValue("id", accountId);
        var balance = await command.ExecuteScalarAsync(cancellationToken);
        return balance is decimal value ? value : throw new InvalidOperationException($"The account {accountId} was not found.");
    }

    public static async Task<Either<Error, TransferResult>> CreateImmediateTransferAsync(
        FinanceTestEnvironment environment,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var createImmediateTransferUseCase = scope.ServiceProvider.GetRequiredService<CreateImmediateTransferUseCase>();
        var command = new CreateImmediateTransferCommand(sourceAccountId, destinationAccountId, amount, idempotencyKey);
        return await createImmediateTransferUseCase.ExecuteAsync(command, cancellationToken);
    }

    public static async Task<Either<Error, TransferResult>> ScheduleTransferAsync(
        FinanceTestEnvironment environment,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        DateTimeOffset scheduledFor,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var scheduleTransferUseCase = scope.ServiceProvider.GetRequiredService<ScheduleTransferUseCase>();
        var command = new ScheduleTransferCommand(sourceAccountId, destinationAccountId, amount, scheduledFor, idempotencyKey);
        return await scheduleTransferUseCase.ExecuteAsync(command, cancellationToken);
    }

    public static async Task<Either<Error, TransferResponse>> CancelTransferAsync(
        FinanceTestEnvironment environment,
        Guid transferId,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var cancelTransferUseCase = scope.ServiceProvider.GetRequiredService<CancelTransferUseCase>();
        return await cancelTransferUseCase.ExecuteAsync(transferId, cancellationToken);
    }

    public static async Task<Either<Error, TransferResponse>> GetTransferAsync(
        FinanceTestEnvironment environment,
        Guid transferId,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var getTransferUseCase = scope.ServiceProvider.GetRequiredService<GetTransferUseCase>();
        return await getTransferUseCase.ExecuteAsync(transferId, cancellationToken);
    }

    public static async Task<Either<Error, CursorPage<TransferResponse>>> ListAccountTransfersAsync(
        FinanceTestEnvironment environment,
        ListAccountTransfersQuery query,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var listAccountTransfersUseCase = scope.ServiceProvider.GetRequiredService<ListAccountTransfersUseCase>();
        return await listAccountTransfersUseCase.ExecuteAsync(query, cancellationToken);
    }

    public static async Task<Either<Error, Unit>> SetTransferLimitsAsync(
        FinanceTestEnvironment environment,
        SetTransferLimitsCommand command,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var setTransferLimitsUseCase = scope.ServiceProvider.GetRequiredService<SetTransferLimitsUseCase>();
        return await setTransferLimitsUseCase.ExecuteAsync(command, cancellationToken);
    }

    public static async Task<Either<Error, TransferLimitsResponse>> GetTransferLimitsAsync(
        FinanceTestEnvironment environment,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var getTransferLimitsUseCase = scope.ServiceProvider.GetRequiredService<GetTransferLimitsUseCase>();
        return await getTransferLimitsUseCase.ExecuteAsync(accountId, cancellationToken);
    }

    public static async Task<DispatchDueScheduledTransfersResult> DispatchDueTransfersAsync(
        FinanceTestEnvironment environment,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var dispatchDueScheduledTransfersUseCase = scope.ServiceProvider.GetRequiredService<DispatchDueScheduledTransfersUseCase>();
        return await dispatchDueScheduledTransfersUseCase.ExecuteAsync(cancellationToken);
    }

    public static async Task<ScheduledTransferExecutionOutcome> ExecuteScheduledTransferAsync(
        FinanceTestEnvironment environment,
        Guid transferId,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var executeScheduledTransferUseCase = scope.ServiceProvider.GetRequiredService<ExecuteScheduledTransferUseCase>();
        return await executeScheduledTransferUseCase.ExecuteAsync(transferId, cancellationToken);
    }

    public static async Task<int> CountOutboxMessagesAsync(
        FinanceTestEnvironment environment,
        string routingKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(environment, cancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM outbox_messages WHERE routing_key = @routingKey", connection);
        command.Parameters.AddWithValue("routingKey", routingKey);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    public static async Task<IReadOnlyList<string>> GetOutboxMessageContentsAsync(
        FinanceTestEnvironment environment,
        string routingKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(environment, cancellationToken);
        await using var command = new NpgsqlCommand("SELECT content::text FROM outbox_messages WHERE routing_key = @routingKey ORDER BY occurred_at", connection);
        command.Parameters.AddWithValue("routingKey", routingKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var contents = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            contents.Add(reader.GetString(0));
        }

        return contents;
    }

    public static Task<int> CountTransfersAsync(FinanceTestEnvironment environment, CancellationToken cancellationToken)
    {
        return CountRowsAsync(environment, "SELECT count(*) FROM transfers", cancellationToken);
    }

    public static Task<int> CountTransferLimitPoliciesAsync(FinanceTestEnvironment environment, CancellationToken cancellationToken)
    {
        return CountRowsAsync(environment, "SELECT count(*) FROM transfer_limit_policies", cancellationToken);
    }

    private static async Task<int> CountRowsAsync(FinanceTestEnvironment environment, string countSql, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(environment, cancellationToken);
        await using var command = new NpgsqlCommand(countSql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<NpgsqlConnection> OpenConnectionAsync(FinanceTestEnvironment environment, CancellationToken cancellationToken)
    {
        var connectionString = environment.Configuration.GetConnectionString("Finance")
            ?? throw new InvalidOperationException("The Finance connection string is not configured.");
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
