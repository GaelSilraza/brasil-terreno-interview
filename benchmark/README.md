# Benchmark (opcional)

Ambiente de carga parecido com produção, separado do `docker-compose.yml` principal. Ele não faz parte dos testes automatizados e nada no projeto depende dele.

## O que sobe

- `gateway`: nginx servindo o front compilado com `VITE_API_URL=/api` e fazendo proxy de `/api/` para a API (porta `8088` do host).
- `api` e `worker` em modo `Production`, logs em `Warning`, CORS liberado apenas para `http://localhost:8088`.
- `postgres` com `pg_stat_statements` (sem porta publicada no host) e `rabbitmq`.
- `migrator` (sem o seed padrão) e `seed`, que carrega 200 mil contas, 5 "contas quentes", 300 mil transferências e 600 mil lançamentos (`seed/seed.sql`).
- `k6` (perfil `load`), que executa `k6/scenarios.js`.

Cada serviço, exceto o k6, tem limite de CPU e memória (`deploy.resources.limits`), somando menos de 3 GB.

## Como rodar

```bash
cd benchmark
./run.sh
```

O script sobe a pilha do zero (projeto `finance-benchmark`), espera o seed, roda os cenários, exporta os relatórios e, no fim, remove containers e volume (`down -v`). A pilha principal (`docker compose` na raiz) deve estar parada para não disputar CPU.

Duração: cerca de 15 minutos (1 a 2 de build e seed, cerca de 12 de carga).

## Resultados

Cada execução cria `results/<data-hora>/` com:

| Arquivo | Conteúdo |
|---|---|
| `summary.md` / `summary.json` | Tabelas por cenário (k6) e dados brutos |
| `environment.md` | Host, versões, limites por container, configuração da API e do worker |
| `docker-stats.csv` / `docker-stats-summary.md` | CPU e memória por container a cada 5 s |
| `top-statements.md` | 15 consultas mais caras do `pg_stat_statements` |
| `database-state.md` | Transferências criadas por status, fila do outbox, cache do Postgres |
| `errors.md` / `k6.log` | Respostas inesperadas agrupadas e o log completo do k6 |

O resumo dos resultados (antes e depois dos ajustes) está na seção [Benchmark](../README.md#benchmark) do README principal.
