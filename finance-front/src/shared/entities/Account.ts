import type { AccountStatus } from '@src/shared/types';

export interface Account {
  id: string;
  personId: string;
  holderName: string;
  documentNumber: string;
  balance: number;
  overdraftLimit: number;
  availableAmount: number;
  usedOverdraft: number;
  status: AccountStatus;
  createdAt: string;
  updatedAt: string;
}
