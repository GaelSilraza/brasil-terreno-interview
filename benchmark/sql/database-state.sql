SELECT '| Indicador | Valor |'
UNION ALL
SELECT '|---|---|'
UNION ALL
SELECT format('| Transferências criadas no teste (%s, %s) | %s |', type, status, count(*))
FROM transfers
WHERE created_at > now() - interval '1 hour'
GROUP BY type, status
UNION ALL
SELECT format('| Falhas por código no teste (%s) | %s |', failure_code, count(*))
FROM transfers
WHERE created_at > now() - interval '1 hour' AND failure_code IS NOT NULL
GROUP BY failure_code
UNION ALL
SELECT format('| Mensagens do outbox ainda não publicadas | %s |', count(*))
FROM outbox_messages
WHERE processed_at IS NULL
UNION ALL
SELECT format('| Mensagens do outbox publicadas | %s |', count(*))
FROM outbox_messages
WHERE processed_at IS NOT NULL
UNION ALL
SELECT format('| Atraso médio de publicação do outbox (s) | %s |', round(avg(extract(epoch FROM processed_at - occurred_at))::numeric, 2))
FROM outbox_messages
WHERE processed_at IS NOT NULL
UNION ALL
SELECT format('| Atraso máximo de publicação do outbox (s) | %s |', round(max(extract(epoch FROM processed_at - occurred_at))::numeric, 2))
FROM outbox_messages
WHERE processed_at IS NOT NULL
UNION ALL
SELECT format('| Taxa de acerto do cache do Postgres | %s%% |', round(100.0 * sum(blks_hit) / nullif(sum(blks_hit) + sum(blks_read), 0), 2))
FROM pg_stat_database
WHERE datname = current_database()
UNION ALL
SELECT format('| Deadlocks | %s |', deadlocks)
FROM pg_stat_database
WHERE datname = current_database()
UNION ALL
SELECT format('| Conexões abertas no fim | %s |', count(*))
FROM pg_stat_activity
WHERE datname = current_database();
