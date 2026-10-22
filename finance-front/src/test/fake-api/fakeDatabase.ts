import type { Account, AccountEntry, Transfer } from '@src/shared/entities';
import type { AccountStatus, TransferStatus } from '@src/shared/types';

export interface IStoredLimitPolicy {
  daytimeMaxAmountPerHour: number;
  daytimeMaxAttemptsPerHour: number;
  nighttimeMaxAmountPerHour: number;
  nighttimeMaxAttemptsPerHour: number;
}

export interface IStoredIdempotentResponse {
  fingerprint: string;
  status: number;
  body: unknown;
}

export interface IFakeDatabase {
  accounts: Account[];
  entries: AccountEntry[];
  transfers: Transfer[];
  limitPolicies: Map<string, IStoredLimitPolicy>;
  idempotentResponses: Map<string, IStoredIdempotentResponse>;
  sequences: Map<string, number>;
  lastSequence: number;
}

interface ISequencedRecord {
  id: string;
}

export const fakeAccountIds = {
  ana: '0199f3c2-0000-7000-8000-000000000001',
  bruno: '0199f3c2-0000-7000-8000-000000000002',
  carla: '0199f3c2-0000-7000-8000-000000000003',
  daniel: '0199f3c2-0000-7000-8000-000000000004',
  elisa: '0199f3c2-0000-7000-8000-000000000005',
} as const;

const seedTimestamp = '2026-10-20T12:00:00.000Z';

function createSeedAccount(
  accountId: string,
  holderName: string,
  documentNumber: string,
  balance: number,
  overdraftLimit: number,
  status: AccountStatus
): Account {
  return {
    id: accountId,
    personId: `person-${accountId}`,
    holderName,
    documentNumber,
    balance,
    overdraftLimit,
    availableAmount: balance + overdraftLimit,
    usedOverdraft: balance < 0 ? -balance : 0,
    status,
    createdAt: seedTimestamp,
    updatedAt: seedTimestamp,
  };
}

function createSeedState(): IFakeDatabase {
  return {
    accounts: [
      createSeedAccount(
        fakeAccountIds.ana,
        'Ana Souza',
        '10000000001',
        500,
        1000,
        'Active'
      ),
      createSeedAccount(
        fakeAccountIds.bruno,
        'Bruno Lima',
        '10000000002',
        -800,
        1000,
        'Active'
      ),
      createSeedAccount(
        fakeAccountIds.carla,
        'Carla Mendes',
        '10000000003',
        10000,
        0,
        'Active'
      ),
      createSeedAccount(
        fakeAccountIds.daniel,
        'Daniel Rocha',
        '10000000004',
        300,
        0,
        'Blocked'
      ),
      createSeedAccount(
        fakeAccountIds.elisa,
        'Elisa Martins',
        '10000000005',
        2000,
        500,
        'Inactive'
      ),
    ],
    entries: [],
    transfers: [],
    limitPolicies: new Map(),
    idempotentResponses: new Map(),
    sequences: new Map(),
    lastSequence: 0,
  };
}

export const fakeDatabase: IFakeDatabase = createSeedState();

export function resetFakeDatabase(): void {
  Object.assign(fakeDatabase, createSeedState());
}

export function sequenceFakeRecords(
  records: readonly ISequencedRecord[]
): void {
  records.forEach((record) => {
    if (!fakeDatabase.sequences.has(record.id)) {
      fakeDatabase.lastSequence += 1;
      fakeDatabase.sequences.set(record.id, fakeDatabase.lastSequence);
    }
  });
}

export function findFakeSequence(recordId: string): number | undefined {
  return fakeDatabase.sequences.get(recordId);
}

export function seedManyFakeAccounts(
  count: number,
  namePrefix = 'Cliente'
): Account[] {
  const seededAccounts = Array.from({ length: count }, (_, position) => {
    const index = position + 1;
    const accountId = `0199f3c2-1000-7000-8000-${String(index).padStart(12, '0')}`;

    return createSeedAccount(
      accountId,
      `${namePrefix} ${String(index).padStart(4, '0')}`,
      '2' + String(index).padStart(10, '0'),
      1000,
      0,
      'Active'
    );
  });

  fakeDatabase.accounts.push(...seededAccounts);
  return seededAccounts;
}

export function findFakeAccount(accountId: string): Account | undefined {
  return fakeDatabase.accounts.find((account) => account.id === accountId);
}

export function setFakeAccountBalance(account: Account, balance: number): void {
  account.balance = balance;
  account.availableAmount = balance + account.overdraftLimit;
  account.usedOverdraft = balance < 0 ? -balance : 0;
  account.updatedAt = new Date().toISOString();
}

export function addFakeTransfer(
  transfer: Pick<
    Transfer,
    'sourceAccountId' | 'destinationAccountId' | 'amount' | 'type' | 'status'
  > &
    Partial<Transfer>
): Transfer {
  const now = new Date().toISOString();
  const storedTransfer: Transfer = {
    id: crypto.randomUUID(),
    sourceHolderName:
      findFakeAccount(transfer.sourceAccountId)?.holderName ?? '',
    destinationHolderName:
      findFakeAccount(transfer.destinationAccountId)?.holderName ?? '',
    scheduledFor: null,
    attemptedAt: transfer.status === 'Scheduled' ? null : now,
    processingStartedAt: null,
    completedAt: transfer.status === 'Completed' ? now : null,
    failedAt: transfer.status === 'Failed' ? now : null,
    cancelledAt: transfer.status === 'Cancelled' ? now : null,
    failureCode: null,
    createdAt: now,
    updatedAt: now,
    ...transfer,
  };
  fakeDatabase.transfers.push(storedTransfer);
  sequenceFakeRecords(fakeDatabase.transfers);
  return storedTransfer;
}

export function setFakeTransferStatus(
  transferId: string,
  status: TransferStatus,
  failureCode: string | null = null
): void {
  const transfer = fakeDatabase.transfers.find(
    (storedTransfer) => storedTransfer.id === transferId
  );

  if (!transfer) {
    throw new Error(`Fake transfer ${transferId} not found.`);
  }

  const now = new Date().toISOString();
  transfer.status = status;
  transfer.failureCode = failureCode;
  transfer.updatedAt = now;
  transfer.completedAt = status === 'Completed' ? now : transfer.completedAt;
  transfer.failedAt = status === 'Failed' ? now : transfer.failedAt;
}
