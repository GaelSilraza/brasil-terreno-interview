import type { AccountStatus } from '@src/shared/types';

export interface UpdateAccountStatusDTO {
  accountId: string;
  status: AccountStatus;
}
