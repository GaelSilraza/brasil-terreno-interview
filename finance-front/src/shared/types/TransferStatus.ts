import type { transferStatuses } from '@src/shared/resources/transferStatuses';

export type TransferStatus = (typeof transferStatuses)[number];
