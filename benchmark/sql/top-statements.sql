SELECT '| Chamadas | Média (ms) | Total (ms) | Linhas | Consulta |'
UNION ALL
SELECT '|---|---|---|---|---|'
UNION ALL
SELECT line
FROM (
    SELECT format(
        '| %s | %s | %s | %s | `%s` |',
        statements.calls,
        round(statements.mean_exec_time::numeric, 3),
        round(statements.total_exec_time::numeric, 1),
        statements.rows,
        replace(left(regexp_replace(statements.query, '\s+', ' ', 'g'), 200), '|', '\|')) AS line
    FROM pg_stat_statements AS statements
    JOIN pg_database AS databases ON databases.oid = statements.dbid
    WHERE databases.datname = current_database()
    ORDER BY statements.total_exec_time DESC
    LIMIT 15
) AS top_statements;
