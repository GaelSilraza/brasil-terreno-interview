#!/usr/bin/env bash
set -euo pipefail

benchmark_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
compose_file="$benchmark_directory/docker-compose.benchmark.yml"
project_name="finance-benchmark"
gateway_url="http://localhost:8088"
timestamp="$(date +%Y%m%d-%H%M%S)"
results_directory="$benchmark_directory/results/$timestamp"
stats_file="$results_directory/docker-stats.csv"
stats_pid=""

export HOST_UID="$(id -u)"
export HOST_GID="$(id -g)"
export RESULTS_DIR="$results_directory"

compose() {
  docker compose -f "$compose_file" -p "$project_name" "$@"
}

stop_stats_loop() {
  if [ -n "$stats_pid" ] && kill -0 "$stats_pid" 2>/dev/null; then
    kill "$stats_pid" 2>/dev/null || true
    wait "$stats_pid" 2>/dev/null || true
  fi
  stats_pid=""
}

cleanup() {
  stop_stats_loop
  echo "Removing the benchmark stack and its volume."
  compose --profile load down -v --remove-orphans >/dev/null 2>&1 || true
}

wait_for_seed() {
  local seed_container
  seed_container="$(compose ps -a -q seed)"
  local exit_code
  exit_code="$(docker wait "$seed_container")"
  if [ "$exit_code" != "0" ]; then
    docker logs "$seed_container" | tail -n 30
    echo "The seed failed with exit code $exit_code." >&2
    exit 1
  fi
  docker logs "$seed_container" | tail -n 8
}

wait_for_gateway() {
  local attempt
  for attempt in $(seq 1 90); do
    if [ "$(curl -s -o /dev/null -w '%{http_code}' "$gateway_url/api/accounts?limit=1")" = "200" ]; then
      echo "Gateway ready after $attempt attempts."
      return 0
    fi
    sleep 2
  done
  echo "The gateway did not answer 200 in time." >&2
  exit 1
}

container_limits_table() {
  echo "| Container | Imagem | CPUs | Memória |"
  echo "|---|---|---|---|"
  local container_id
  for container_id in $(compose ps -a -q); do
    docker inspect --format '{{.Name}}|{{.Config.Image}}|{{.HostConfig.NanoCpus}}|{{.HostConfig.Memory}}' "$container_id" |
      awk -F'|' '{
        name = substr($1, 2);
        cpus = ($3 == 0) ? "sem limite" : sprintf("%.2f", $3 / 1000000000);
        memory = ($4 == 0) ? "sem limite" : sprintf("%d MiB", $4 / 1048576);
        printf "| %s | %s | %s | %s |\n", name, $2, cpus, memory;
      }'
  done
}

application_settings() {
  local service
  for service in api worker; do
    echo "### $service"
    echo
    echo '```'
    docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$(compose ps -q "$service")" |
      grep -E '^(ASPNETCORE_|DOTNET_|Logging__|Cors__|OpenApi__|Outbox__|ScheduledTransfers__|RabbitMq__HostName|ConnectionStrings__)' |
      sed -E 's/Password=[^;]*/Password=***/'
    echo '```'
    echo
  done
}

write_environment() {
  {
    echo "# Ambiente da execução $timestamp"
    echo
    echo "- CPUs do host (nproc): $(nproc)"
    echo "- Memória do host: $(LC_ALL=C free -h | awk '/^Mem:/ {print $2 " total, " $7 " disponível no início"}')"
    echo "- Kernel: $(uname -r)"
    echo "- Docker: $(docker version --format '{{.Server.Version}}')"
    echo "- Docker Compose: $(docker compose version --short)"
    echo "- k6: grafana/k6:2.3.0 (sem limite de recursos, compartilha a CPU do host)"
    echo
    echo "## Limites por container"
    echo
    container_limits_table
    echo
    echo "## Configuração da API e do worker"
    echo
    application_settings
  } > "$results_directory/environment.md"
}

start_stats_loop() {
  echo "timestamp,container,cpu_percent,memory_usage" > "$stats_file"
  (
    while true; do
      containers="$(docker ps --filter "name=^${project_name}-" --format '{{.Names}}' | tr '\n' ' ')"
      if [ -n "$containers" ]; then
        now="$(date +%H:%M:%S)"
        docker stats --no-stream --format '{{.Name}},{{.CPUPerc}},{{.MemUsage}}' $containers 2>/dev/null |
          sed "s/^/$now,/" >> "$stats_file" || true
      fi
      sleep 5
    done
  ) &
  stats_pid=$!
}

write_stats_summary() {
  {
    echo "| Container | Amostras | CPU média | CPU máxima | Memória máxima |"
    echo "|---|---|---|---|---|"
    tail -n +2 "$stats_file" | awk -F',' '{
      container = $2;
      cpu = $3; gsub("%", "", cpu);
      split($4, memoryParts, " / ");
      memory = memoryParts[1];
      value = memory + 0;
      if (memory ~ /GiB/) { value = value * 1024 } else if (memory ~ /KiB/) { value = value / 1024 } else if (memory ~ /^[0-9.]+B$/) { value = value / 1048576 }
      samples[container]++;
      cpuTotal[container] += cpu;
      if (cpu + 0 > cpuMaximum[container] + 0) { cpuMaximum[container] = cpu }
      if (value > memoryMaximum[container]) { memoryMaximum[container] = value }
    }
    END {
      for (container in samples) {
        printf "| %s | %d | %.1f%% | %.1f%% | %.0f MiB |\n", container, samples[container], cpuTotal[container] / samples[container], cpuMaximum[container], memoryMaximum[container];
      }
    }' | sort
  } > "$results_directory/docker-stats-summary.md"
}

export_database_reports() {
  compose exec -T postgres psql -U finance -d finance -At -v ON_ERROR_STOP=1 < "$benchmark_directory/sql/top-statements.sql" > "$results_directory/top-statements.md"
  compose exec -T postgres psql -U finance -d finance -At -v ON_ERROR_STOP=1 < "$benchmark_directory/sql/database-state.sql" > "$results_directory/database-state.md"
}

write_error_breakdown() {
  {
    echo "| Ocorrências | Resposta inesperada |"
    echo "|---|---|"
    grep -o 'unexpected-response [^"]*' "$results_directory/k6.log" | sed 's/unexpected-response //' | sort | uniq -c | sort -rn |
      awk '{count = $1; $1 = ""; sub(/^ /, ""); printf "| %s | %s |\n", count, $0}' || true
  } > "$results_directory/errors.md"
}

mkdir -p "$results_directory"
trap cleanup EXIT

echo "Results: $results_directory"
compose --profile load down -v --remove-orphans >/dev/null 2>&1 || true
compose up -d --build postgres rabbitmq migrator seed api worker gateway
wait_for_seed
wait_for_gateway
write_environment
start_stats_loop

k6_exit_code=0
compose --profile load run --rm -T k6 run --no-color /scripts/scenarios.js 2>&1 | tee "$results_directory/k6.log" || k6_exit_code=$?

export_database_reports
stop_stats_loop
write_stats_summary
write_error_breakdown

echo "k6 exit code: $k6_exit_code (99 means that at least one threshold was crossed)." | tee "$results_directory/k6-exit-code.txt"
echo "Results written to $results_directory"
