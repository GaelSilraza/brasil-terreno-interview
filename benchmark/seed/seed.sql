CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

SELECT count(*) > 1000 AS already_seeded FROM persons \gset

\if :already_seeded
\echo 'Benchmark data already present: nothing to seed.'
\else
\echo 'Seeding 200000 accounts, 5 hot accounts, 300000 transfers, 600000 ledger entries and one opening entry per account.'

BEGIN;

CREATE TEMPORARY TABLE seed_first_names ON COMMIT DROP AS
SELECT names.position::integer AS position, names.first_name
FROM unnest(ARRAY[
    'Alice', 'Amanda', 'Ana', 'Beatriz', 'Bernardo', 'Bruno', 'Caio', 'Camila', 'Carla', 'Daniel',
    'Davi', 'Diego', 'Eduardo', 'Elisa', 'Enzo', 'Fabio', 'Felipe', 'Fernanda', 'Gabriel', 'Giovana',
    'Gustavo', 'Heitor', 'Helena', 'Henrique', 'Igor', 'Ingrid', 'Isabela', 'Joao', 'Jose', 'Julia',
    'Karina', 'Larissa', 'Leonardo', 'Livia', 'Lorena', 'Lucas', 'Marcos', 'Mariana', 'Matheus', 'Miguel',
    'Natalia', 'Nicolas', 'Olivia', 'Otavio', 'Patricia', 'Paulo', 'Pedro', 'Rafaela', 'Renato', 'Rodrigo',
    'Sabrina', 'Samuel', 'Sofia', 'Tatiana', 'Thiago', 'Valentina', 'Vanessa', 'Vitor', 'Wesley', 'Yasmin'
]) WITH ORDINALITY AS names (first_name, position);

CREATE TEMPORARY TABLE seed_surnames ON COMMIT DROP AS
SELECT names.position::integer AS position, names.surname
FROM unnest(ARRAY[
    'Almeida', 'Alves', 'Araujo', 'Barbosa', 'Batista', 'Campos', 'Cardoso', 'Carvalho', 'Castro', 'Cavalcanti',
    'Correia', 'Costa', 'Dias', 'Duarte', 'Farias', 'Fernandes', 'Ferreira', 'Fonseca', 'Freitas', 'Garcia',
    'Gomes', 'Leite', 'Lima', 'Lopes', 'Macedo', 'Machado', 'Marques', 'Martins', 'Medeiros', 'Melo',
    'Mendes', 'Miranda', 'Monteiro', 'Moreira', 'Moura', 'Nascimento', 'Nogueira', 'Nunes', 'Oliveira', 'Pacheco',
    'Peixoto', 'Pereira', 'Pinto', 'Queiroz', 'Ramos', 'Reis', 'Rezende', 'Ribeiro', 'Rocha', 'Rodrigues',
    'Sampaio', 'Santos', 'Silva', 'Siqueira', 'Souza', 'Tavares', 'Teixeira', 'Vasconcelos', 'Vieira', 'Xavier'
]) WITH ORDINALITY AS names (surname, position);

CREATE TEMPORARY TABLE seed_holders ON COMMIT DROP AS
SELECT
    series.holder_number,
    uuidv7(interval '-30 days') AS person_id,
    uuidv7(interval '-30 days') AS account_id,
    concat_ws(' ', first_names.first_name, surnames.surname, lpad(series.holder_number::text, 6, '0')) AS full_name,
    lpad(series.holder_number::text, 11, '0') AS document_number,
    round((random() * 50000)::numeric, 2) AS balance,
    round((random() * 5000)::numeric, 2) AS overdraft_limit,
    CASE
        WHEN series.status_draw < 0.01 THEN 'Blocked'
        WHEN series.status_draw < 0.02 THEN 'Inactive'
        ELSE 'Active'
    END AS status
FROM (
    SELECT numbers.holder_number, random() AS status_draw
    FROM generate_series(1, 200000) AS numbers (holder_number)
) AS series
JOIN seed_first_names AS first_names ON first_names.position = series.holder_number % 60 + 1
JOIN seed_surnames AS surnames ON surnames.position = (series.holder_number / 60) % 60 + 1;

INSERT INTO persons (id, full_name, document_number, created_at, updated_at)
SELECT person_id, full_name, document_number, now() - interval '30 days', now() - interval '30 days'
FROM seed_holders;

INSERT INTO accounts (id, person_id, overdraft_limit, status, created_at, updated_at)
SELECT account_id, person_id, overdraft_limit, status, now() - interval '30 days', now() - interval '30 days'
FROM seed_holders;

CREATE TEMPORARY TABLE seed_hot_accounts ON COMMIT DROP AS
SELECT
    series.hot_number,
    uuidv7(interval '-30 days') AS person_id,
    uuidv7(interval '-30 days') AS account_id
FROM generate_series(1, 5) AS series (hot_number);

INSERT INTO persons (id, full_name, document_number, created_at, updated_at)
SELECT person_id, 'Conta Quente ' || hot_number, '9999999999' || hot_number, now() - interval '30 days', now() - interval '30 days'
FROM seed_hot_accounts;

INSERT INTO accounts (id, person_id, overdraft_limit, status, created_at, updated_at)
SELECT account_id, person_id, 0.00, 'Active', now() - interval '30 days', now() - interval '30 days'
FROM seed_hot_accounts;

INSERT INTO transfer_limit_policies (
    account_id,
    daytime_max_amount_per_hour,
    daytime_max_attempts_per_hour,
    nighttime_max_amount_per_hour,
    nighttime_max_attempts_per_hour,
    updated_at)
SELECT account_id, 1000000000.00, 1000000, 1000000000.00, 1000000, now()
FROM seed_hot_accounts;

CREATE TEMPORARY TABLE seed_active_accounts ON COMMIT DROP AS
SELECT (row_number() OVER (ORDER BY account_id) - 1) AS account_index, account_id
FROM seed_holders
WHERE status = 'Active';

CREATE UNIQUE INDEX ON seed_active_accounts (account_index);

SELECT count(*) AS active_account_count FROM seed_active_accounts \gset

CREATE TEMPORARY TABLE seed_history ON COMMIT DROP AS
SELECT
    uuidv7(-draws.age) AS transfer_id,
    uuidv7(-draws.age) AS debit_entry_id,
    uuidv7(-draws.age) AS credit_entry_id,
    sources.account_id AS source_account_id,
    destinations.account_id AS destination_account_id,
    draws.amount,
    now() - draws.age AS attempted_at
FROM (
    SELECT
        series.history_number,
        interval '3 hours' + random() * interval '60 days' AS age,
        round((1 + random() * 499)::numeric, 2) AS amount,
        floor(random() * :active_account_count)::bigint AS source_index,
        floor(random() * (:active_account_count - 1))::bigint AS destination_offset
    FROM generate_series(1, 300000) AS series (history_number)
) AS draws
JOIN seed_active_accounts AS sources ON sources.account_index = draws.source_index
JOIN seed_active_accounts AS destinations
    ON destinations.account_index = (draws.source_index + 1 + draws.destination_offset) % :active_account_count;

INSERT INTO transfers (
    id,
    source_account_id,
    destination_account_id,
    amount,
    type,
    status,
    scheduled_for,
    attempted_at,
    processing_started_at,
    completed_at,
    failed_at,
    cancelled_at,
    failure_code,
    idempotency_key,
    request_fingerprint,
    created_at,
    updated_at)
SELECT
    transfer_id,
    source_account_id,
    destination_account_id,
    amount,
    'Immediate',
    'Completed',
    NULL,
    attempted_at,
    attempted_at,
    attempted_at,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    attempted_at,
    attempted_at
FROM seed_history;

INSERT INTO account_entries (id, account_id, transfer_id, counterpart_account_id, entry_type, amount, balance_after, created_at)
SELECT debit_entry_id, source_account_id, transfer_id, destination_account_id, 'Debit', amount, round((random() * 50000)::numeric, 2), attempted_at
FROM seed_history
UNION ALL
SELECT credit_entry_id, destination_account_id, transfer_id, source_account_id, 'Credit', amount, round((random() * 50000)::numeric, 2), attempted_at
FROM seed_history;

INSERT INTO account_entries (id, account_id, transfer_id, counterpart_account_id, entry_type, amount, balance_after, created_at)
SELECT
    uuidv7(interval '-61 days'),
    opening.account_id,
    NULL,
    NULL,
    CASE WHEN opening.opening_balance < 0 THEN 'Debit' ELSE 'Credit' END,
    abs(opening.opening_balance),
    opening.opening_balance,
    now() - interval '61 days'
FROM (
    SELECT
        targets.account_id,
        targets.balance - COALESCE(movements.net_amount, 0) AS opening_balance
    FROM (
        SELECT account_id, balance FROM seed_holders
        UNION ALL
        SELECT account_id, 1000000000.00 FROM seed_hot_accounts
    ) AS targets
    LEFT JOIN (
        SELECT account_id, sum(CASE WHEN entry_type = 'Credit' THEN amount ELSE -amount END) AS net_amount
        FROM account_entries
        GROUP BY account_id
    ) AS movements ON movements.account_id = targets.account_id
) AS opening
WHERE opening.opening_balance <> 0;

COMMIT;

VACUUM (ANALYZE);

SELECT
    (SELECT count(*) FROM persons) AS persons,
    (SELECT count(*) FROM accounts) AS accounts,
    (SELECT count(*) FROM accounts WHERE status <> 'Active') AS inactive_or_blocked_accounts,
    (SELECT count(*) FROM transfers) AS transfers,
    (SELECT count(*) FROM account_entries) AS account_entries,
    (SELECT count(*) FROM transfer_limit_policies) AS limit_policies;
\endif

SELECT pg_stat_statements_reset();
