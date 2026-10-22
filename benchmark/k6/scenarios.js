import http from 'k6/http';
import { check, sleep } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

const baseUrl = __ENV.BASE_URL || 'http://gateway';
const allowedOrigin = __ENV.ALLOWED_ORIGIN || 'http://localhost:8088';
const forbiddenOrigin = 'https://evil.example';
const resultsDirectory = __ENV.RESULTS_DIRECTORY || '/results';
const apiUrl = `${baseUrl}/api`;
const jsonHeaders = { 'Content-Type': 'application/json' };

const poolSize = 2000;
const hotAccountCount = 5;
const pagesPerWalk = 50;
const pageBuckets = ['01-10', '11-20', '21-30', '31-40', '41-50'];
const readEndpoints = ['account', 'entries', 'transfers', 'limits'];
const finalTransferStatuses = ['Completed', 'Failed', 'Cancelled'];

const firstNames = [
  'Alice', 'Amanda', 'Ana', 'Beatriz', 'Bernardo', 'Bruno', 'Caio', 'Camila', 'Carla', 'Daniel',
  'Davi', 'Diego', 'Eduardo', 'Elisa', 'Enzo', 'Fabio', 'Felipe', 'Fernanda', 'Gabriel', 'Giovana',
  'Gustavo', 'Heitor', 'Helena', 'Henrique', 'Igor', 'Ingrid', 'Isabela', 'Joao', 'Jose', 'Julia',
  'Karina', 'Larissa', 'Leonardo', 'Livia', 'Lorena', 'Lucas', 'Marcos', 'Mariana', 'Matheus', 'Miguel',
  'Natalia', 'Nicolas', 'Olivia', 'Otavio', 'Patricia', 'Paulo', 'Pedro', 'Rafaela', 'Renato', 'Rodrigo',
  'Sabrina', 'Samuel', 'Sofia', 'Tatiana', 'Thiago', 'Valentina', 'Vanessa', 'Vitor', 'Wesley', 'Yasmin',
];

const surnames = [
  'Almeida', 'Alves', 'Araujo', 'Barbosa', 'Batista', 'Campos', 'Cardoso', 'Carvalho', 'Castro', 'Cavalcanti',
  'Correia', 'Costa', 'Dias', 'Duarte', 'Farias', 'Fernandes', 'Ferreira', 'Fonseca', 'Freitas', 'Garcia',
  'Gomes', 'Leite', 'Lima', 'Lopes', 'Macedo', 'Machado', 'Marques', 'Martins', 'Medeiros', 'Melo',
  'Mendes', 'Miranda', 'Monteiro', 'Moreira', 'Moura', 'Nascimento', 'Nogueira', 'Nunes', 'Oliveira', 'Pacheco',
  'Peixoto', 'Pereira', 'Pinto', 'Queiroz', 'Ramos', 'Reis', 'Rezende', 'Ribeiro', 'Rocha', 'Rodrigues',
  'Sampaio', 'Santos', 'Silva', 'Siqueira', 'Souza', 'Tavares', 'Teixeira', 'Vasconcelos', 'Vieira', 'Xavier',
];

const scenarioPlan = [
  {
    name: 'account_search',
    title: 'Busca de contas (combobox)',
    target: 150,
    configuration: {
      executor: 'ramping-arrival-rate',
      exec: 'accountSearch',
      startTime: '0s',
      startRate: 20,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 300,
      stages: [{ target: 150, duration: '2m' }],
    },
  },
  {
    name: 'accounts_deep_pagination',
    title: 'Paginação profunda por cursor',
    target: 100,
    configuration: {
      executor: 'constant-vus',
      exec: 'accountsDeepPagination',
      startTime: '2m10s',
      vus: 10,
      duration: '1m',
    },
  },
  {
    name: 'immediate_transfers',
    title: 'Transferências imediatas',
    target: 250,
    configuration: {
      executor: 'constant-arrival-rate',
      exec: 'immediateTransfers',
      startTime: '3m20s',
      rate: 80,
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 100,
      maxVUs: 400,
    },
  },
  {
    name: 'hot_account_contention',
    title: 'Contenção em contas quentes',
    target: 800,
    configuration: {
      executor: 'constant-vus',
      exec: 'hotAccountContention',
      startTime: '5m30s',
      vus: 40,
      duration: '1m',
    },
  },
  {
    name: 'idempotent_retries',
    title: 'Repetições idempotentes',
    target: null,
    configuration: {
      executor: 'constant-vus',
      exec: 'idempotentRetries',
      startTime: '6m40s',
      vus: 10,
      duration: '30s',
    },
  },
  {
    name: 'scheduled_throughput',
    title: 'Agendadas de ponta a ponta',
    target: null,
    configuration: {
      executor: 'shared-iterations',
      exec: 'scheduledThroughput',
      startTime: '7m20s',
      vus: 20,
      iterations: 300,
      maxDuration: '2m30s',
    },
  },
  {
    name: 'reads',
    title: 'Leituras',
    target: 150,
    configuration: {
      executor: 'constant-arrival-rate',
      exec: 'reads',
      startTime: '10m',
      rate: 100,
      timeUnit: '1s',
      duration: '1m',
      preAllocatedVUs: 50,
      maxVUs: 300,
    },
  },
  {
    name: 'cors',
    title: 'Política de CORS',
    target: null,
    configuration: {
      executor: 'per-vu-iterations',
      exec: 'corsPolicy',
      startTime: '11m10s',
      vus: 1,
      iterations: 20,
      maxDuration: '1m',
    },
  },
];

const paginationPageDuration = new Trend('pagination_page_duration', true);
const scheduledCompletionSeconds = new Trend('scheduled_completion_seconds');
const scheduledDelayAfterDueSeconds = new Trend('scheduled_delay_after_due_seconds');
const scenarioClock = new Trend('scenario_clock');
const unexpectedResponses = new Counter('unexpected_responses');
const scheduledTimeouts = new Counter('scheduled_timeouts');

function buildThresholds() {
  const thresholds = {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    scheduled_completion_seconds: ['p(95)<10'],
    scheduled_delay_after_due_seconds: ['max>=0'],
    scheduled_timeouts: ['count>=0'],
  };

  for (const scenario of scenarioPlan) {
    const durationThreshold = scenario.target === null ? 'max>=0' : `p(95)<${scenario.target}`;
    thresholds[`http_req_duration{scenario:${scenario.name}}`] = [durationThreshold];
    thresholds[`http_reqs{scenario:${scenario.name}}`] = ['count>=0'];
    thresholds[`http_req_failed{scenario:${scenario.name}}`] = ['rate>=0'];
    thresholds[`checks{scenario:${scenario.name}}`] = [scenario.name === 'cors' ? 'rate==1' : 'rate>=0'];
    thresholds[`scenario_clock{scenario:${scenario.name}}`] = ['max>=0'];
    thresholds[`unexpected_responses{scenario:${scenario.name}}`] = ['count>=0'];
  }

  for (const bucket of pageBuckets) {
    thresholds[`pagination_page_duration{pages:${bucket}}`] = ['max>=0'];
  }

  for (const endpoint of readEndpoints) {
    thresholds[`http_req_duration{scenario:reads,endpoint:${endpoint}}`] = ['max>=0'];
  }

  return thresholds;
}

function buildScenarios() {
  const scenarios = {};

  for (const scenario of scenarioPlan) {
    scenarios[scenario.name] = scenario.configuration;
  }

  return scenarios;
}

export const options = {
  setupTimeout: '5m',
  discardResponseBodies: false,
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max', 'count'],
  scenarios: buildScenarios(),
  thresholds: buildThresholds(),
};

http.setResponseCallback(http.expectedStatuses(200, 201, 204));

function randomInteger(minimum, maximum) {
  return Math.floor(Math.random() * (maximum - minimum + 1)) + minimum;
}

function randomItem(items) {
  return items[Math.floor(Math.random() * items.length)];
}

function randomAmount() {
  return Math.round((1 + Math.random() * 49) * 100) / 100;
}

function pickDistinctPair(accounts) {
  const sourceIndex = Math.floor(Math.random() * accounts.length);
  const offset = 1 + Math.floor(Math.random() * (accounts.length - 1));
  return [accounts[sourceIndex], accounts[(sourceIndex + offset) % accounts.length]];
}

function pickDestination(accounts, sourceAccountId) {
  let destinationAccountId = randomItem(accounts);

  while (destinationAccountId === sourceAccountId) {
    destinationAccountId = randomItem(accounts);
  }

  return destinationAccountId;
}

function uniqueKey(prefix) {
  return `${prefix}-${exec.vu.idInTest}-${exec.scenario.iterationInTest}-${Date.now()}-${randomInteger(0, 999999)}`;
}

function markScenarioClock() {
  scenarioClock.add(Date.now() / 1000);
}

function readJson(response, selector) {
  try {
    return response.json(selector);
  } catch (error) {
    return null;
  }
}

function recordUnexpectedResponse(response, expectedStatus) {
  if (response.status === expectedStatus) {
    return;
  }

  const code = readJson(response, 'code') || 'none';
  unexpectedResponses.add(1, { status: String(response.status), code: code });
  console.warn(`unexpected-response scenario=${exec.scenario.name} status=${response.status} code=${code}`);
}

function accountsUrl(search, cursor, limit) {
  const parameters = [`limit=${limit}`];

  if (search) {
    parameters.push(`search=${encodeURIComponent(search)}`);
  }

  if (cursor) {
    parameters.push(`cursor=${encodeURIComponent(cursor)}`);
  }

  return `${apiUrl}/accounts?${parameters.join('&')}`;
}

function postTransfer(path, body, idempotencyKey, endpointName) {
  return http.post(`${apiUrl}${path}`, JSON.stringify(body), {
    headers: Object.assign({ 'Idempotency-Key': idempotencyKey }, jsonHeaders),
    tags: { name: endpointName },
  });
}

function randomSearchTerm() {
  if (Math.random() < 0.7) {
    const word = Math.random() < 0.5 ? randomItem(firstNames) : randomItem(surnames);
    const length = randomInteger(3, Math.min(6, word.length));
    const start = randomInteger(0, word.length - length);
    return word.substring(start, start + length).toLowerCase();
  }

  const documentNumber = String(randomInteger(1, 200000)).padStart(11, '0');
  return documentNumber.substring(0, randomInteger(3, 8));
}

export function setup() {
  const pool = [];
  let cursor = null;

  while (pool.length < poolSize) {
    const response = http.get(accountsUrl(null, cursor, 100), { tags: { name: 'setup accounts' } });

    if (response.status !== 200) {
      throw new Error(`Account pool request failed with status ${response.status}`);
    }

    const page = response.json();

    for (const account of page.items) {
      if (account.status === 'Active' && account.availableAmount >= 1000 && !account.holderName.startsWith('Conta Quente')) {
        pool.push(account.id);
      }
    }

    if (!page.nextCursor) {
      break;
    }

    cursor = page.nextCursor;
  }

  const hotResponse = http.get(accountsUrl('Conta Quente', null, 10), { tags: { name: 'setup hot accounts' } });
  const hotAccounts = hotResponse.json('items').map((account) => account.id);

  if (hotAccounts.length !== hotAccountCount) {
    throw new Error(`Expected ${hotAccountCount} hot accounts, found ${hotAccounts.length}`);
  }

  const limitsBody = JSON.stringify({
    daytimeMaxAmountPerHour: 100000000,
    daytimeMaxAttemptsPerHour: 1000,
    nighttimeMaxAmountPerHour: 100000000,
    nighttimeMaxAttemptsPerHour: 1000,
  });
  const poolAccounts = pool.slice(0, poolSize);

  for (let start = 0; start < poolAccounts.length; start += 50) {
    const requests = poolAccounts.slice(start, start + 50).map((accountId) => ({
      method: 'PUT',
      url: `${apiUrl}/transfer-limits/${accountId}`,
      body: limitsBody,
      params: { headers: jsonHeaders, tags: { name: 'setup transfer limits' } },
    }));
    const responses = http.batch(requests);

    for (const response of responses) {
      if (response.status !== 204) {
        throw new Error(`Raising transfer limits failed with status ${response.status}`);
      }
    }
  }

  return { pool: poolAccounts, hotAccounts: hotAccounts };
}

export function accountSearch() {
  markScenarioClock();
  const term = randomSearchTerm();
  const response = http.get(accountsUrl(term, null, 20), { tags: { name: 'GET /api/accounts?search' } });
  recordUnexpectedResponse(response, 200);
  const succeeded = check(response, { 'search returns 200': (searchResponse) => searchResponse.status === 200 });

  if (succeeded && Math.random() < 0.3) {
    const nextCursor = readJson(response, 'nextCursor');

    if (nextCursor) {
      const nextResponse = http.get(accountsUrl(term, nextCursor, 20), { tags: { name: 'GET /api/accounts?search&cursor' } });
      recordUnexpectedResponse(nextResponse, 200);
      check(nextResponse, { 'next search page returns 200': (searchResponse) => searchResponse.status === 200 });
    }
  }

  markScenarioClock();
}

export function accountsDeepPagination() {
  markScenarioClock();
  let cursor = null;

  for (let page = 1; page <= pagesPerWalk; page++) {
    const bucket = pageBuckets[Math.floor((page - 1) / 10)];
    const response = http.get(accountsUrl(null, cursor, 50), { tags: { name: 'GET /api/accounts?cursor', pages: bucket } });
    paginationPageDuration.add(response.timings.duration, { pages: bucket });
    recordUnexpectedResponse(response, 200);

    if (!check(response, { 'page returns 200': (pageResponse) => pageResponse.status === 200 })) {
      break;
    }

    cursor = readJson(response, 'nextCursor');

    if (!cursor) {
      break;
    }
  }

  markScenarioClock();
}

export function immediateTransfers(data) {
  markScenarioClock();
  const [sourceAccountId, destinationAccountId] = pickDistinctPair(data.pool);
  const body = { sourceAccountId: sourceAccountId, destinationAccountId: destinationAccountId, amount: randomAmount() };
  const response = postTransfer('/transfers', body, uniqueKey('immediate'), 'POST /api/transfers');
  recordUnexpectedResponse(response, 201);
  check(response, {
    'immediate transfer returns 201': (transferResponse) => transferResponse.status === 201,
    'immediate transfer is completed': (transferResponse) => readJson(transferResponse, 'status') === 'Completed',
  });
  markScenarioClock();
}

export function hotAccountContention(data) {
  markScenarioClock();
  const sourceAccountId = randomItem(data.hotAccounts);
  const body = { sourceAccountId: sourceAccountId, destinationAccountId: randomItem(data.pool), amount: randomAmount() };
  const response = postTransfer('/transfers', body, uniqueKey('hot'), 'POST /api/transfers (hot)');
  recordUnexpectedResponse(response, 201);
  check(response, {
    'hot transfer returns 201': (transferResponse) => transferResponse.status === 201,
    'hot transfer is completed': (transferResponse) => readJson(transferResponse, 'status') === 'Completed',
  });
  markScenarioClock();
}

export function idempotentRetries(data) {
  markScenarioClock();
  const [sourceAccountId, destinationAccountId] = pickDistinctPair(data.pool);
  const body = { sourceAccountId: sourceAccountId, destinationAccountId: destinationAccountId, amount: randomAmount() };
  const idempotencyKey = uniqueKey('retry');
  const firstResponse = postTransfer('/transfers', body, idempotencyKey, 'POST /api/transfers (first)');
  recordUnexpectedResponse(firstResponse, 201);
  const replayResponse = postTransfer('/transfers', body, idempotencyKey, 'POST /api/transfers (replay)');
  recordUnexpectedResponse(replayResponse, 201);
  const firstTransferId = readJson(firstResponse, 'id');
  check(firstResponse, { 'first request returns 201': (transferResponse) => transferResponse.status === 201 });
  check(replayResponse, {
    'replay returns 201': (transferResponse) => transferResponse.status === 201,
    'replay carries Idempotent-Replayed': (transferResponse) => transferResponse.headers['Idempotent-Replayed'] === 'true',
    'replay returns the same transfer': (transferResponse) => firstTransferId !== null && readJson(transferResponse, 'id') === firstTransferId,
  });
  markScenarioClock();
}

export function scheduledThroughput(data) {
  markScenarioClock();
  const [sourceAccountId, destinationAccountId] = pickDistinctPair(data.pool);
  const createdAt = Date.now();
  const scheduledFor = new Date(createdAt + 5000);
  const body = {
    sourceAccountId: sourceAccountId,
    destinationAccountId: destinationAccountId,
    amount: randomAmount(),
    scheduledFor: scheduledFor.toISOString(),
  };
  const response = postTransfer('/transfers/scheduled', body, uniqueKey('scheduled'), 'POST /api/transfers/scheduled');
  recordUnexpectedResponse(response, 201);
  const scheduled = check(response, {
    'scheduled transfer returns 201': (transferResponse) => transferResponse.status === 201,
    'scheduled transfer is Scheduled': (transferResponse) => readJson(transferResponse, 'status') === 'Scheduled',
  });

  if (!scheduled) {
    markScenarioClock();
    return;
  }

  const transferId = readJson(response, 'id');
  let finalStatus = null;

  while (Date.now() - createdAt < 60000) {
    sleep(0.5);
    const pollResponse = http.get(`${apiUrl}/transfers/${transferId}`, { tags: { name: 'GET /api/transfers/{id}' } });
    recordUnexpectedResponse(pollResponse, 200);
    const status = readJson(pollResponse, 'status');

    if (finalTransferStatuses.includes(status)) {
      finalStatus = status;
      break;
    }
  }

  const finishedAt = Date.now();

  if (finalStatus === null) {
    scheduledTimeouts.add(1);
  } else {
    scheduledCompletionSeconds.add((finishedAt - createdAt) / 1000);
    scheduledDelayAfterDueSeconds.add((finishedAt - scheduledFor.getTime()) / 1000);
  }

  check(finalStatus, { 'scheduled transfer completes within 60 s': (status) => status === 'Completed' });
  markScenarioClock();
}

export function reads(data) {
  markScenarioClock();
  const accountId = Math.random() < 0.1 ? randomItem(data.hotAccounts) : randomItem(data.pool);
  const endpoint = randomItem(readEndpoints);
  const urls = {
    account: `${apiUrl}/accounts/${accountId}`,
    entries: `${apiUrl}/accounts/${accountId}/entries?limit=20`,
    transfers: `${apiUrl}/transfers?accountId=${accountId}&limit=20`,
    limits: `${apiUrl}/transfer-limits/${accountId}`,
  };
  const response = http.get(urls[endpoint], { tags: { name: `GET ${endpoint}`, endpoint: endpoint } });
  recordUnexpectedResponse(response, 200);
  check(response, { 'read returns 200': (readResponse) => readResponse.status === 200 });
  markScenarioClock();
}

export function corsPolicy() {
  markScenarioClock();
  const preflightHeaders = {
    'Access-Control-Request-Method': 'POST',
    'Access-Control-Request-Headers': 'content-type,idempotency-key',
  };
  const allowedPreflight = http.options(`${apiUrl}/transfers`, null, {
    headers: Object.assign({ Origin: allowedOrigin }, preflightHeaders),
    tags: { name: 'OPTIONS /api/transfers (allowed origin)' },
  });
  const forbiddenPreflight = http.options(`${apiUrl}/transfers`, null, {
    headers: Object.assign({ Origin: forbiddenOrigin }, preflightHeaders),
    tags: { name: 'OPTIONS /api/transfers (forbidden origin)' },
  });
  const allowedRead = http.get(accountsUrl(null, null, 1), {
    headers: { Origin: allowedOrigin },
    tags: { name: 'GET /api/accounts (allowed origin)' },
  });
  const forbiddenRead = http.get(accountsUrl(null, null, 1), {
    headers: { Origin: forbiddenOrigin },
    tags: { name: 'GET /api/accounts (forbidden origin)' },
  });

  check(allowedPreflight, {
    'allowed preflight returns 204': (response) => response.status === 204,
    'allowed preflight echoes the gateway origin': (response) => response.headers['Access-Control-Allow-Origin'] === allowedOrigin,
    'allowed preflight allows POST': (response) => (response.headers['Access-Control-Allow-Methods'] || '').includes('POST'),
  });
  check(forbiddenPreflight, {
    'forbidden preflight has no Access-Control-Allow-Origin': (response) => response.headers['Access-Control-Allow-Origin'] === undefined,
  });
  check(allowedRead, {
    'allowed read returns 200': (response) => response.status === 200,
    'allowed read carries Access-Control-Allow-Origin': (response) => response.headers['Access-Control-Allow-Origin'] === allowedOrigin,
  });
  check(forbiddenRead, {
    'forbidden read has no Access-Control-Allow-Origin': (response) => response.headers['Access-Control-Allow-Origin'] === undefined,
  });
  markScenarioClock();
}

function metricValues(data, name) {
  const metric = data.metrics[name];
  return metric ? metric.values : null;
}

function thresholdsPassed(data, name) {
  const metric = data.metrics[name];

  if (!metric || !metric.thresholds) {
    return null;
  }

  return Object.values(metric.thresholds).every((threshold) => threshold.ok);
}

function formatNumber(value, decimals) {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '-';
  }

  return Number(value).toFixed(decimals);
}

function formatPercent(rate) {
  if (rate === null || rate === undefined) {
    return '-';
  }

  return `${(rate * 100).toFixed(2)}%`;
}

function scenarioRow(data, scenario) {
  const durations = metricValues(data, `http_req_duration{scenario:${scenario.name}}`);
  const requests = metricValues(data, `http_reqs{scenario:${scenario.name}}`);
  const failures = metricValues(data, `http_req_failed{scenario:${scenario.name}}`);
  const checks = metricValues(data, `checks{scenario:${scenario.name}}`);
  const clock = metricValues(data, `scenario_clock{scenario:${scenario.name}}`);
  const requestCount = requests ? requests.count : 0;
  const elapsedSeconds = clock && clock.max > clock.min ? clock.max - clock.min : null;
  const requestRate = elapsedSeconds ? requestCount / elapsedSeconds : null;
  const target = scenario.target === null ? '-' : `< ${scenario.target} ms`;
  const targetMet = scenario.target === null ? '-' : thresholdsPassed(data, `http_req_duration{scenario:${scenario.name}}`) ? 'sim' : 'não';

  return [
    scenario.name,
    String(requestCount),
    formatNumber(requestRate, 1),
    formatPercent(failures ? failures.rate : null),
    formatNumber(durations ? durations.med : null, 1),
    formatNumber(durations ? durations['p(90)'] : null, 1),
    formatNumber(durations ? durations['p(95)'] : null, 1),
    formatNumber(durations ? durations['p(99)'] : null, 1),
    formatNumber(durations ? durations.max : null, 1),
    target,
    targetMet,
    formatPercent(checks ? checks.rate : null),
  ];
}

function markdownTable(header, rows) {
  const lines = [`| ${header.join(' | ')} |`, `|${header.map(() => '---').join('|')}|`];

  for (const row of rows) {
    lines.push(`| ${row.join(' | ')} |`);
  }

  return lines.join('\n');
}

function trendRow(label, values, decimals) {
  if (!values || !values.count) {
    return [label, '-', '-', '-', '-', '-', '-', '-'];
  }

  return [
    label,
    String(values.count),
    formatNumber(values.avg, decimals),
    formatNumber(values.med, decimals),
    formatNumber(values['p(90)'], decimals),
    formatNumber(values['p(95)'], decimals),
    formatNumber(values['p(99)'], decimals),
    formatNumber(values.max, decimals),
  ];
}

function buildMarkdown(data) {
  const sections = [];
  const scenarioHeader = ['Cenário', 'Requisições', 'req/s', 'Erros HTTP', 'p50 (ms)', 'p90 (ms)', 'p95 (ms)', 'p99 (ms)', 'máx (ms)', 'Meta p95', 'Meta atingida', 'Checks'];
  const trendHeader = ['Métrica', 'Amostras', 'média', 'p50', 'p90', 'p95', 'p99', 'máx'];
  const globalFailures = metricValues(data, 'http_req_failed');
  const globalChecks = metricValues(data, 'checks');
  const totalRequests = metricValues(data, 'http_reqs');
  const unexpectedRows = scenarioPlan.map((scenario) => {
    const values = metricValues(data, `unexpected_responses{scenario:${scenario.name}}`);
    return [scenario.name, String(values ? values.count : 0)];
  });

  sections.push('# Resultado do benchmark (k6)');
  sections.push(`Duração total: ${formatNumber(data.state.testRunDurationMs / 1000, 0)} s. Requisições: ${totalRequests ? totalRequests.count : 0}. Erros HTTP: ${formatPercent(globalFailures ? globalFailures.rate : null)} (meta < 1%: ${thresholdsPassed(data, 'http_req_failed') ? 'sim' : 'não'}). Checks: ${formatPercent(globalChecks ? globalChecks.rate : null)} (meta > 99%: ${thresholdsPassed(data, 'checks') ? 'sim' : 'não'}).`);
  sections.push('## Cenários');
  sections.push(markdownTable(scenarioHeader, scenarioPlan.map((scenario) => scenarioRow(data, scenario))));
  sections.push('## Paginação profunda por faixa de páginas (ms)');
  sections.push(markdownTable(trendHeader, pageBuckets.map((bucket) => trendRow(`páginas ${bucket}`, metricValues(data, `pagination_page_duration{pages:${bucket}}`), 1))));
  sections.push('## Leituras por endpoint (ms)');
  sections.push(markdownTable(trendHeader, readEndpoints.map((endpoint) => trendRow(endpoint, metricValues(data, `http_req_duration{scenario:reads,endpoint:${endpoint}}`), 1))));
  sections.push('## Agendadas de ponta a ponta (s)');
  sections.push(markdownTable(trendHeader, [
    trendRow('criação → status final (scheduled_completion_seconds)', metricValues(data, 'scheduled_completion_seconds'), 2),
    trendRow('vencimento → status final', metricValues(data, 'scheduled_delay_after_due_seconds'), 2),
  ]));
  const timeouts = metricValues(data, 'scheduled_timeouts');
  const completions = metricValues(data, 'scheduled_completion_seconds');
  const scheduledTargetMet = completions && completions.count > 0 && thresholdsPassed(data, 'scheduled_completion_seconds');
  sections.push(`Agendadas com status final: ${completions ? completions.count : 0}. Agendadas sem status final em 60 s: ${timeouts ? timeouts.count : 0}. Meta p95 < 10 s atingida: ${scheduledTargetMet ? 'sim' : 'não'}.`);
  sections.push('## Respostas inesperadas por cenário');
  sections.push(markdownTable(['Cenário', 'Respostas inesperadas'], unexpectedRows));

  return `${sections.join('\n\n')}\n`;
}

export function handleSummary(data) {
  const markdown = buildMarkdown(data);
  const files = {};
  files[`${resultsDirectory}/summary.json`] = JSON.stringify(data, null, 2);
  files[`${resultsDirectory}/summary.md`] = markdown;
  files.stdout = markdown;
  return files;
}
