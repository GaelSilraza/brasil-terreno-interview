import { http, HttpResponse, type HttpHandler } from 'msw';

import type {
  Account,
  AccountEntry,
  CursorPage,
  Transfer,
  TransferLimits,
} from '@src/shared/entities';
import { accountStatuses } from '@src/shared/resources';
import type { AccountStatus } from '@src/shared/types';
import { hasAtMostTwoDecimals } from '@src/shared/utils/money';
import { apiUrl } from '@src/test/apiUrl';

import {
  decodeFakeCursor,
  encodeFakeCursor,
  isRecord,
  paginateByKeyset,
  readCursorQuery,
  type IFakeCursorQuery,
} from './cursorPagination';
import {
  fakeDatabase,
  findFakeAccount,
  findFakeSequence,
  sequenceFakeRecords,
  setFakeAccountBalance,
  type IStoredLimitPolicy,
} from './fakeDatabase';
import {
  createProblemBody,
  createValidationProblemBody,
  problemResponse,
  validationProblemResponse,
  type IFakeFieldError,
} from './problems';

interface IAccountParams {
  accountId: string;
}

interface ITransferParams {
  transferId: string;
}

interface IFakeResponse {
  status: number;
  body: unknown;
  persisted: boolean;
}

interface ITransferRequest {
  sourceAccountId: string;
  destinationAccountId: string;
  amount: number;
}

interface IAccountCursorKey {
  fullName: string;
  personId: string;
}

interface ISequencedFakeRecord {
  id: string;
}

const defaultLimitPolicy: IStoredLimitPolicy = {
  daytimeMaxAmountPerHour: 5000,
  daytimeMaxAttemptsPerHour: 5,
  nighttimeMaxAmountPerHour: 1000,
  nighttimeMaxAttemptsPerHour: 3,
};

const oneHourInMilliseconds = 60 * 60 * 1000;

const saoPauloHourFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: 'America/Sao_Paulo',
  hour: 'numeric',
  hourCycle: 'h23',
});

function isInSaoPauloDaytime(date: Date): boolean {
  const hour = Number(saoPauloHourFormatter.format(date));
  return hour >= 6 && hour < 20;
}

const maximumSearchLength = 100;
const maximumDocumentSearchDigits = 11;
const maximumIdempotencyKeyLength = 100;

const accountOrderCollator = new Intl.Collator('pt-BR', {
  sensitivity: 'variant',
});

function compareAccountKeys(
  firstKey: IAccountCursorKey,
  secondKey: IAccountCursorKey
): number {
  return (
    accountOrderCollator.compare(firstKey.fullName, secondKey.fullName) ||
    accountOrderCollator.compare(firstKey.personId, secondKey.personId)
  );
}

function accountKeyOf(account: Account): IAccountCursorKey {
  return { fullName: account.holderName, personId: account.personId };
}

function decodeAccountCursor(cursor: string): IAccountCursorKey | null {
  const decodedCursor = decodeFakeCursor(cursor);

  if (
    isRecord(decodedCursor) &&
    typeof decodedCursor.fullName === 'string' &&
    typeof decodedCursor.personId === 'string'
  ) {
    return {
      fullName: decodedCursor.fullName,
      personId: decodedCursor.personId,
    };
  }

  return null;
}

function decodeSequenceCursor(cursor: string): number | null {
  const decodedCursor = decodeFakeCursor(cursor);

  if (isRecord(decodedCursor) && typeof decodedCursor.id === 'string') {
    return findFakeSequence(decodedCursor.id) ?? null;
  }

  return null;
}

function sequenceOf(record: ISequencedFakeRecord): number {
  return findFakeSequence(record.id) ?? 0;
}

function isDocumentNumberSearch(search: string): boolean {
  const digitCount = search.replace(/\D/g, '').length;
  return (
    /^[\d.-]+$/.test(search) &&
    digitCount >= 1 &&
    digitCount <= maximumDocumentSearchDigits
  );
}

function matchesAccountSearch(account: Account, search: string): boolean {
  if (search === '') {
    return true;
  }

  if (isDocumentNumberSearch(search)) {
    return account.documentNumber.startsWith(search.replace(/\D/g, ''));
  }

  return account.holderName
    .toLocaleLowerCase('pt-BR')
    .includes(search.toLocaleLowerCase('pt-BR'));
}

function cursorValidationResponse(): HttpResponse<Record<string, unknown>> {
  return validationProblemResponse([{ field: 'cursor', code: 'InvalidValue' }]);
}

function paginateNewestFirst<TItem extends ISequencedFakeRecord>(
  records: TItem[],
  query: IFakeCursorQuery
): CursorPage<TItem> | null {
  sequenceFakeRecords(records);
  const sortedRecords = [...records].sort(
    (firstRecord, secondRecord) =>
      sequenceOf(secondRecord) - sequenceOf(firstRecord)
  );
  const cursorSequence =
    query.cursor === null ? null : decodeSequenceCursor(query.cursor);

  if (query.cursor !== null && cursorSequence === null) {
    return null;
  }

  return paginateByKeyset({
    sortedItems: sortedRecords,
    query,
    compareToCursor:
      cursorSequence === null
        ? null
        : (record) => cursorSequence - sequenceOf(record),
    cursorOf: (record) => encodeFakeCursor({ id: record.id }),
  });
}

function roundToCents(value: number): number {
  return Math.round(value * 100) / 100;
}

async function readJsonBody(
  request: Request
): Promise<Record<string, unknown>> {
  try {
    const body: unknown = await request.json();
    return typeof body === 'object' && body !== null
      ? (body as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
}

function readString(body: Record<string, unknown>, key: string): string {
  const value = body[key];
  return typeof value === 'string' ? value : '';
}

function readNumber(body: Record<string, unknown>, key: string): number {
  const value = body[key];
  return typeof value === 'number' ? value : Number.NaN;
}

function validateMoney(
  fieldErrors: IFakeFieldError[],
  field: string,
  value: number,
  allowZero: boolean
): void {
  const isInRange = allowZero ? value >= 0 : value > 0;

  if (!isInRange) {
    fieldErrors.push({
      field,
      code: 'OutOfRange',
      params: {
        comparisonValue: 0,
        comparison: allowZero ? 'greaterThanOrEqual' : 'greaterThan',
      },
    });
    return;
  }

  if (!hasAtMostTwoDecimals(value)) {
    fieldErrors.push({
      field,
      code: 'InvalidPrecision',
      params: { expectedPrecision: 18, expectedScale: 2 },
    });
  }
}

function validateAttemptLimit(
  fieldErrors: IFakeFieldError[],
  field: string,
  value: number
): void {
  if (!(Number.isInteger(value) && value >= 1 && value <= 1000)) {
    fieldErrors.push({
      field,
      code: 'OutOfRange',
      params: { from: 1, to: 1000 },
    });
  }
}

function createValidationResponse(
  fieldErrors: readonly IFakeFieldError[]
): IFakeResponse {
  return {
    status: 400,
    body: createValidationProblemBody(fieldErrors),
    persisted: false,
  };
}

function toHttpResponse(
  status: number,
  body: unknown,
  isReplay: boolean
): HttpResponse<string> {
  const headers = new Headers({
    'Content-Type':
      status >= 400 ? 'application/problem+json' : 'application/json',
  });

  if (status === 201) {
    headers.set('Location', `/api/transfers/${(body as Transfer).id}`);
  }

  if (isReplay) {
    headers.set('Idempotent-Replayed', 'true');
  }

  return new HttpResponse(JSON.stringify(body), { status, headers });
}

function handleIdempotentRequest(
  request: Request,
  fingerprint: string,
  execute: () => IFakeResponse
): HttpResponse<string> {
  const idempotencyKey = request.headers.get('Idempotency-Key');

  if (idempotencyKey) {
    const storedResponse = fakeDatabase.idempotentResponses.get(idempotencyKey);

    if (storedResponse && storedResponse.fingerprint === fingerprint) {
      return toHttpResponse(storedResponse.status, storedResponse.body, true);
    }

    if (storedResponse) {
      return toHttpResponse(
        422,
        createProblemBody(422, 'IdempotencyKeyReused'),
        false
      );
    }
  }

  const fakeResponse = execute();

  if (idempotencyKey && fakeResponse.persisted) {
    fakeDatabase.idempotentResponses.set(idempotencyKey, {
      fingerprint,
      status: fakeResponse.status,
      body: fakeResponse.body,
    });
  }

  return toHttpResponse(fakeResponse.status, fakeResponse.body, false);
}

function readTransferRequest(body: Record<string, unknown>): ITransferRequest {
  return {
    sourceAccountId: readString(body, 'sourceAccountId'),
    destinationAccountId: readString(body, 'destinationAccountId'),
    amount: readNumber(body, 'amount'),
  };
}

function validateTransferRequest(
  transferRequest: ITransferRequest,
  idempotencyKey: string | null
): IFakeFieldError[] {
  const fieldErrors: IFakeFieldError[] = [];

  if (!transferRequest.sourceAccountId) {
    fieldErrors.push({ field: 'sourceAccountId', code: 'Required' });
  }

  if (!transferRequest.destinationAccountId) {
    fieldErrors.push({ field: 'destinationAccountId', code: 'Required' });
  } else if (
    transferRequest.sourceAccountId === transferRequest.destinationAccountId
  ) {
    fieldErrors.push({
      field: 'destinationAccountId',
      code: 'MustBeDifferent',
    });
  }

  validateMoney(fieldErrors, 'amount', transferRequest.amount, false);

  if (
    idempotencyKey !== null &&
    idempotencyKey.length > maximumIdempotencyKeyLength
  ) {
    fieldErrors.push({
      field: 'idempotencyKey',
      code: 'InvalidLength',
      params: { minLength: 1, maxLength: maximumIdempotencyKeyLength },
    });
  }

  return fieldErrors;
}

function buildTransfer(
  source: Account,
  destination: Account,
  amount: number,
  details: Pick<Transfer, 'type' | 'status'> & Partial<Transfer>
): Transfer {
  const now = new Date().toISOString();

  return {
    id: crypto.randomUUID(),
    sourceAccountId: source.id,
    sourceHolderName: source.holderName,
    destinationAccountId: destination.id,
    destinationHolderName: destination.holderName,
    amount,
    scheduledFor: null,
    attemptedAt: null,
    processingStartedAt: null,
    completedAt: null,
    failedAt: null,
    cancelledAt: null,
    failureCode: null,
    createdAt: now,
    updatedAt: now,
    ...details,
  };
}

function createNotFoundResponse(code: string): IFakeResponse {
  return { status: 404, body: createProblemBody(404, code), persisted: false };
}

function findInactiveAccountCode(
  source: Account,
  destination: Account
): string | null {
  if (source.status !== 'Active') {
    return 'SourceAccountInactive';
  }

  if (destination.status !== 'Active') {
    return 'DestinationAccountInactive';
  }

  return null;
}

function createEntry(
  account: Account,
  counterpart: Account,
  transferId: string,
  entryType: AccountEntry['entryType'],
  amount: number,
  createdAt: string
): AccountEntry {
  return {
    id: crypto.randomUUID(),
    accountId: account.id,
    transferId,
    counterpartAccountId: counterpart.id,
    counterpartHolderName: counterpart.holderName,
    entryType,
    amount,
    balanceAfter: account.balance,
    createdAt,
  };
}

function executeImmediateTransfer(
  transferRequest: ITransferRequest,
  idempotencyKey: string | null
): IFakeResponse {
  const fieldErrors = validateTransferRequest(transferRequest, idempotencyKey);

  if (fieldErrors.length > 0) {
    return createValidationResponse(fieldErrors);
  }

  const source = findFakeAccount(transferRequest.sourceAccountId);
  const destination = findFakeAccount(transferRequest.destinationAccountId);

  if (!source) {
    return createNotFoundResponse('SourceAccountNotFound');
  }

  if (!destination) {
    return createNotFoundResponse('DestinationAccountNotFound');
  }

  const now = new Date().toISOString();
  const { amount } = transferRequest;
  const failureCode =
    findInactiveAccountCode(source, destination) ??
    (amount > source.availableAmount ? 'InsufficientFunds' : null);

  if (failureCode) {
    const failedTransfer = buildTransfer(source, destination, amount, {
      type: 'Immediate',
      status: 'Failed',
      attemptedAt: now,
      processingStartedAt: now,
      failedAt: now,
      failureCode,
    });
    fakeDatabase.transfers.push(failedTransfer);
    sequenceFakeRecords(fakeDatabase.transfers);

    return {
      status: 422,
      body: createProblemBody(422, failureCode, {
        transferId: failedTransfer.id,
      }),
      persisted: true,
    };
  }

  const completedTransfer = buildTransfer(source, destination, amount, {
    type: 'Immediate',
    status: 'Completed',
    attemptedAt: now,
    processingStartedAt: now,
    completedAt: now,
  });

  setFakeAccountBalance(source, roundToCents(source.balance - amount));
  setFakeAccountBalance(
    destination,
    roundToCents(destination.balance + amount)
  );
  fakeDatabase.transfers.push(completedTransfer);
  sequenceFakeRecords(fakeDatabase.transfers);
  fakeDatabase.entries.push(
    createEntry(
      source,
      destination,
      completedTransfer.id,
      'Debit',
      amount,
      now
    ),
    createEntry(
      destination,
      source,
      completedTransfer.id,
      'Credit',
      amount,
      now
    )
  );
  sequenceFakeRecords(fakeDatabase.entries);

  return { status: 201, body: completedTransfer, persisted: true };
}

function executeScheduledTransfer(
  transferRequest: ITransferRequest,
  scheduledFor: string,
  idempotencyKey: string | null
): IFakeResponse {
  const fieldErrors = validateTransferRequest(transferRequest, idempotencyKey);
  const scheduledDate = new Date(scheduledFor);

  if (scheduledFor === '') {
    fieldErrors.push({ field: 'scheduledFor', code: 'Required' });
  } else if (Number.isNaN(scheduledDate.getTime())) {
    fieldErrors.push({ field: 'scheduledFor', code: 'InvalidValue' });
  } else if (scheduledDate <= new Date()) {
    fieldErrors.push({ field: 'scheduledFor', code: 'MustBeInFuture' });
  }

  if (fieldErrors.length > 0) {
    return createValidationResponse(fieldErrors);
  }

  const source = findFakeAccount(transferRequest.sourceAccountId);
  const destination = findFakeAccount(transferRequest.destinationAccountId);

  if (!source) {
    return createNotFoundResponse('SourceAccountNotFound');
  }

  if (!destination) {
    return createNotFoundResponse('DestinationAccountNotFound');
  }

  const inactiveAccountCode = findInactiveAccountCode(source, destination);

  if (inactiveAccountCode) {
    return {
      status: 422,
      body: createProblemBody(422, inactiveAccountCode),
      persisted: false,
    };
  }

  const scheduledTransfer = buildTransfer(
    source,
    destination,
    transferRequest.amount,
    {
      type: 'Scheduled',
      status: 'Scheduled',
      scheduledFor: scheduledDate.toISOString(),
    }
  );
  fakeDatabase.transfers.push(scheduledTransfer);
  sequenceFakeRecords(fakeDatabase.transfers);

  return { status: 201, body: scheduledTransfer, persisted: true };
}

function buildTransferLimits(accountId: string): TransferLimits {
  const customPolicy = fakeDatabase.limitPolicies.get(accountId);
  const policy = customPolicy ?? defaultLimitPolicy;
  const now = new Date();
  const windowStart = now.getTime() - oneHourInMilliseconds;
  const attemptsInLastHour = fakeDatabase.transfers.filter(
    (transfer) =>
      transfer.sourceAccountId === accountId &&
      transfer.attemptedAt !== null &&
      new Date(transfer.attemptedAt).getTime() >= windowStart
  );
  const completedAmountInLastHour = roundToCents(
    attemptsInLastHour
      .filter((transfer) => transfer.status === 'Completed')
      .reduce((total, transfer) => total + transfer.amount, 0)
  );
  const isDaytime = isInSaoPauloDaytime(now);
  const maxAmountPerHour = isDaytime
    ? policy.daytimeMaxAmountPerHour
    : policy.nighttimeMaxAmountPerHour;
  const maxAttemptsPerHour = isDaytime
    ? policy.daytimeMaxAttemptsPerHour
    : policy.nighttimeMaxAttemptsPerHour;

  return {
    accountId,
    isCustom: customPolicy !== undefined,
    currentPeriod: isDaytime ? 'Daytime' : 'Nighttime',
    daytime: {
      maxAmountPerHour: policy.daytimeMaxAmountPerHour,
      maxAttemptsPerHour: policy.daytimeMaxAttemptsPerHour,
    },
    nighttime: {
      maxAmountPerHour: policy.nighttimeMaxAmountPerHour,
      maxAttemptsPerHour: policy.nighttimeMaxAttemptsPerHour,
    },
    usage: {
      attemptsInLastHour: attemptsInLastHour.length,
      completedAmountInLastHour,
      remainingAttempts: Math.max(
        maxAttemptsPerHour - attemptsInLastHour.length,
        0
      ),
      remainingAmount: Math.max(
        roundToCents(maxAmountPerHour - completedAmountInLastHour),
        0
      ),
    },
  };
}

function isAccountStatus(value: string): value is AccountStatus {
  return accountStatuses.some((accountStatus) => accountStatus === value);
}

export const fakeApiHandlers: HttpHandler[] = [
  http.get(`${apiUrl}/accounts`, ({ request }) => {
    const searchParams = new URL(request.url).searchParams;
    const { query, fieldErrors } = readCursorQuery(searchParams);
    const search = (searchParams.get('search') ?? '').trim();

    if (search.length > maximumSearchLength) {
      fieldErrors.push({
        field: 'search',
        code: 'InvalidLength',
        params: { minLength: 0, maxLength: maximumSearchLength },
      });
    }

    if (fieldErrors.length > 0) {
      return validationProblemResponse(fieldErrors);
    }

    const cursorKey =
      query.cursor === null ? null : decodeAccountCursor(query.cursor);

    if (query.cursor !== null && cursorKey === null) {
      return cursorValidationResponse();
    }

    const sortedAccounts = fakeDatabase.accounts
      .filter((account) => matchesAccountSearch(account, search))
      .sort((firstAccount, secondAccount) =>
        compareAccountKeys(
          accountKeyOf(firstAccount),
          accountKeyOf(secondAccount)
        )
      );

    return HttpResponse.json(
      paginateByKeyset({
        sortedItems: sortedAccounts,
        query,
        compareToCursor:
          cursorKey === null
            ? null
            : (account) => compareAccountKeys(accountKeyOf(account), cursorKey),
        cursorOf: (account) => encodeFakeCursor(accountKeyOf(account)),
      })
    );
  }),

  http.get<IAccountParams>(`${apiUrl}/accounts/:accountId`, ({ params }) => {
    const account = findFakeAccount(params.accountId);

    if (!account) {
      return problemResponse(404, 'AccountNotFound');
    }

    return HttpResponse.json(account);
  }),

  http.post(`${apiUrl}/accounts`, async ({ request }) => {
    const body = await readJsonBody(request);
    const fullName = readString(body, 'fullName').trim();
    const rawDocumentNumber = readString(body, 'documentNumber').trim();
    const initialBalance = readNumber(body, 'initialBalance');
    const overdraftLimit = readNumber(body, 'overdraftLimit');
    const fieldErrors: IFakeFieldError[] = [];

    if (fullName === '') {
      fieldErrors.push({ field: 'fullName', code: 'Required' });
    } else if (fullName.length < 3 || fullName.length > 150) {
      fieldErrors.push({
        field: 'fullName',
        code: 'InvalidLength',
        params: { minLength: 3, maxLength: 150 },
      });
    }

    if (rawDocumentNumber === '') {
      fieldErrors.push({ field: 'documentNumber', code: 'Required' });
    } else if (
      !/^\d{11}$/.test(rawDocumentNumber) &&
      !/^\d{3}\.\d{3}\.\d{3}-\d{2}$/.test(rawDocumentNumber)
    ) {
      fieldErrors.push({ field: 'documentNumber', code: 'InvalidFormat' });
    }

    validateMoney(fieldErrors, 'initialBalance', initialBalance, true);
    validateMoney(fieldErrors, 'overdraftLimit', overdraftLimit, true);

    if (fieldErrors.length > 0) {
      return validationProblemResponse(fieldErrors);
    }

    const documentNumber = rawDocumentNumber.replace(/\D/g, '');

    const documentNumberExists = fakeDatabase.accounts.some(
      (account) => account.documentNumber === documentNumber
    );

    if (documentNumberExists) {
      return problemResponse(409, 'DocumentNumberAlreadyExists');
    }

    const now = new Date().toISOString();
    const account: Account = {
      id: crypto.randomUUID(),
      personId: crypto.randomUUID(),
      holderName: fullName,
      documentNumber,
      balance: initialBalance,
      overdraftLimit,
      availableAmount: roundToCents(initialBalance + overdraftLimit),
      usedOverdraft: 0,
      status: 'Active',
      createdAt: now,
      updatedAt: now,
    };
    fakeDatabase.accounts.push(account);

    if (initialBalance !== 0) {
      fakeDatabase.entries.push({
        id: crypto.randomUUID(),
        accountId: account.id,
        transferId: null,
        counterpartAccountId: null,
        counterpartHolderName: null,
        entryType: initialBalance < 0 ? 'Debit' : 'Credit',
        amount: Math.abs(initialBalance),
        balanceAfter: initialBalance,
        createdAt: now,
      });
      sequenceFakeRecords(fakeDatabase.entries);
    }

    return HttpResponse.json(account, {
      status: 201,
      headers: { Location: `/api/accounts/${account.id}` },
    });
  }),

  http.get<IAccountParams>(
    `${apiUrl}/accounts/:accountId/entries`,
    ({ params, request }) => {
      const { query, fieldErrors } = readCursorQuery(
        new URL(request.url).searchParams
      );

      if (fieldErrors.length > 0) {
        return validationProblemResponse(fieldErrors);
      }

      sequenceFakeRecords(fakeDatabase.entries);
      const accountEntries = fakeDatabase.entries.filter(
        (entry) => entry.accountId === params.accountId
      );
      const entriesPage = paginateNewestFirst(accountEntries, query);

      return entriesPage === null
        ? cursorValidationResponse()
        : HttpResponse.json(entriesPage);
    }
  ),

  http.put<IAccountParams>(
    `${apiUrl}/accounts/:accountId/overdraft-limit`,
    async ({ params, request }) => {
      const account = findFakeAccount(params.accountId);

      if (!account) {
        return problemResponse(404, 'AccountNotFound');
      }

      const body = await readJsonBody(request);
      const overdraftLimit = readNumber(body, 'overdraftLimit');

      const fieldErrors: IFakeFieldError[] = [];
      validateMoney(fieldErrors, 'overdraftLimit', overdraftLimit, true);

      if (fieldErrors.length > 0) {
        return validationProblemResponse(fieldErrors);
      }

      account.overdraftLimit = overdraftLimit;
      setFakeAccountBalance(account, account.balance);

      return new HttpResponse(null, { status: 204 });
    }
  ),

  http.put<IAccountParams>(
    `${apiUrl}/accounts/:accountId/status`,
    async ({ params, request }) => {
      const account = findFakeAccount(params.accountId);

      if (!account) {
        return problemResponse(404, 'AccountNotFound');
      }

      const body = await readJsonBody(request);
      const status = readString(body, 'status');

      if (!isAccountStatus(status)) {
        return validationProblemResponse([
          { field: 'status', code: 'InvalidOption' },
        ]);
      }

      account.status = status;
      account.updatedAt = new Date().toISOString();

      return new HttpResponse(null, { status: 204 });
    }
  ),

  http.post(`${apiUrl}/transfers`, async ({ request }) => {
    const body = await readJsonBody(request);
    const transferRequest = readTransferRequest(body);
    const fingerprint = JSON.stringify([
      'Immediate',
      transferRequest.sourceAccountId,
      transferRequest.destinationAccountId,
      transferRequest.amount,
    ]);

    return handleIdempotentRequest(request, fingerprint, () =>
      executeImmediateTransfer(
        transferRequest,
        request.headers.get('Idempotency-Key')
      )
    );
  }),

  http.post(`${apiUrl}/transfers/scheduled`, async ({ request }) => {
    const body = await readJsonBody(request);
    const transferRequest = readTransferRequest(body);
    const scheduledFor = readString(body, 'scheduledFor');
    const fingerprint = JSON.stringify([
      'Scheduled',
      transferRequest.sourceAccountId,
      transferRequest.destinationAccountId,
      transferRequest.amount,
      scheduledFor,
    ]);

    return handleIdempotentRequest(request, fingerprint, () =>
      executeScheduledTransfer(
        transferRequest,
        scheduledFor,
        request.headers.get('Idempotency-Key')
      )
    );
  }),

  http.post<ITransferParams>(
    `${apiUrl}/transfers/:transferId/cancel`,
    ({ params }) => {
      const transfer = fakeDatabase.transfers.find(
        (storedTransfer) => storedTransfer.id === params.transferId
      );

      if (!transfer) {
        return problemResponse(404, 'TransferNotFound');
      }

      if (transfer.status !== 'Scheduled') {
        return problemResponse(409, 'TransferCannotBeCancelled');
      }

      const now = new Date().toISOString();
      transfer.status = 'Cancelled';
      transfer.cancelledAt = now;
      transfer.updatedAt = now;

      return HttpResponse.json(transfer);
    }
  ),

  http.get<ITransferParams>(`${apiUrl}/transfers/:transferId`, ({ params }) => {
    const transfer = fakeDatabase.transfers.find(
      (storedTransfer) => storedTransfer.id === params.transferId
    );

    if (!transfer) {
      return problemResponse(404, 'TransferNotFound');
    }

    return HttpResponse.json(transfer);
  }),

  http.get(`${apiUrl}/transfers`, ({ request }) => {
    const searchParams = new URL(request.url).searchParams;
    const accountId = searchParams.get('accountId');
    const { query, fieldErrors } = readCursorQuery(searchParams);

    if (!accountId) {
      fieldErrors.push({ field: 'accountId', code: 'Required' });
    }

    if (fieldErrors.length > 0) {
      return validationProblemResponse(fieldErrors);
    }

    sequenceFakeRecords(fakeDatabase.transfers);
    const accountTransfers = fakeDatabase.transfers.filter(
      (transfer) =>
        transfer.sourceAccountId === accountId ||
        transfer.destinationAccountId === accountId
    );
    const transfersPage = paginateNewestFirst(accountTransfers, query);

    return transfersPage === null
      ? cursorValidationResponse()
      : HttpResponse.json(transfersPage);
  }),

  http.get<IAccountParams>(
    `${apiUrl}/transfer-limits/:accountId`,
    ({ params }) => {
      if (!findFakeAccount(params.accountId)) {
        return problemResponse(404, 'AccountNotFound');
      }

      return HttpResponse.json(buildTransferLimits(params.accountId));
    }
  ),

  http.put<IAccountParams>(
    `${apiUrl}/transfer-limits/:accountId`,
    async ({ params, request }) => {
      if (!findFakeAccount(params.accountId)) {
        return problemResponse(404, 'AccountNotFound');
      }

      const body = await readJsonBody(request);
      const policy: IStoredLimitPolicy = {
        daytimeMaxAmountPerHour: readNumber(body, 'daytimeMaxAmountPerHour'),
        daytimeMaxAttemptsPerHour: readNumber(
          body,
          'daytimeMaxAttemptsPerHour'
        ),
        nighttimeMaxAmountPerHour: readNumber(
          body,
          'nighttimeMaxAmountPerHour'
        ),
        nighttimeMaxAttemptsPerHour: readNumber(
          body,
          'nighttimeMaxAttemptsPerHour'
        ),
      };
      const fieldErrors: IFakeFieldError[] = [];
      validateMoney(
        fieldErrors,
        'daytimeMaxAmountPerHour',
        policy.daytimeMaxAmountPerHour,
        false
      );
      validateAttemptLimit(
        fieldErrors,
        'daytimeMaxAttemptsPerHour',
        policy.daytimeMaxAttemptsPerHour
      );
      validateMoney(
        fieldErrors,
        'nighttimeMaxAmountPerHour',
        policy.nighttimeMaxAmountPerHour,
        false
      );
      validateAttemptLimit(
        fieldErrors,
        'nighttimeMaxAttemptsPerHour',
        policy.nighttimeMaxAttemptsPerHour
      );

      if (fieldErrors.length > 0) {
        return validationProblemResponse(fieldErrors);
      }

      fakeDatabase.limitPolicies.set(params.accountId, policy);

      return new HttpResponse(null, { status: 204 });
    }
  ),
];
