import type { TransferStatus, TransferType } from '@src/shared/types';

export interface Transfer {
  id: string;
  sourceAccountId: string;
  sourceHolderName: string;
  destinationAccountId: string;
  destinationHolderName: string;
  amount: number;
  type: TransferType;
  status: TransferStatus;
  scheduledFor: string | null;
  attemptedAt: string | null;
  processingStartedAt: string | null;
  completedAt: string | null;
  failedAt: string | null;
  cancelledAt: string | null;
  failureCode: string | null;
  createdAt: string;
  updatedAt: string;
}
