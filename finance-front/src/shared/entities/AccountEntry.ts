import type { AccountEntryType } from '@src/shared/types';

export interface AccountEntry {
  id: string;
  accountId: string;
  transferId: string | null;
  counterpartAccountId: string | null;
  counterpartHolderName: string | null;
  entryType: AccountEntryType;
  amount: number;
  balanceAfter: number;
  createdAt: string;
}
