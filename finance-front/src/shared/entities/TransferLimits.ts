import type { TransferPeriod } from '@src/shared/types';

export interface PeriodLimit {
  maxAmountPerHour: number;
  maxAttemptsPerHour: number;
}

export interface TransferLimitUsage {
  attemptsInLastHour: number;
  completedAmountInLastHour: number;
  remainingAttempts: number;
  remainingAmount: number;
}

export interface TransferLimits {
  accountId: string;
  isCustom: boolean;
  currentPeriod: TransferPeriod;
  daytime: PeriodLimit;
  nighttime: PeriodLimit;
  usage: TransferLimitUsage;
}
