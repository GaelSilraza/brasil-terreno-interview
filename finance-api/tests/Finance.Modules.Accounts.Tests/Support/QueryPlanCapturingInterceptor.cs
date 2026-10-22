using System.Collections.Concurrent;
using System.Data.Common;
using Finance.Modules.Accounts.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Finance.Modules.Accounts.Tests.Support;

public sealed class QueryPlanCapturingInterceptor : DbCommandInterceptor
{
    public ConcurrentQueue<string> Plans { get; } = new();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(AccountQueries.SearchQueryTag, StringComparison.Ordinal) && command.Connection is NpgsqlConnection connection)
        {
            await using (var disableSequentialScan = new NpgsqlCommand("SET enable_seqscan = off", connection, (NpgsqlTransaction?)command.Transaction))
            {
                await disableSequentialScan.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var explain = new NpgsqlCommand("EXPLAIN " + command.CommandText, connection, (NpgsqlTransaction?)command.Transaction);

            foreach (NpgsqlParameter parameter in command.Parameters)
            {
                explain.Parameters.Add(parameter.Clone());
            }

            var planLines = new List<string>();
            await using (var reader = await explain.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    planLines.Add(reader.GetString(0));
                }
            }

            Plans.Enqueue(string.Join('\n', planLines));

            await using var restoreSequentialScan = new NpgsqlCommand("RESET enable_seqscan", connection, (NpgsqlTransaction?)command.Transaction);
            await restoreSequentialScan.ExecuteNonQueryAsync(cancellationToken);
        }

        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
