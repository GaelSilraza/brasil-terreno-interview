import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { Transfer } from '@src/shared/entities';

import { transfersService } from '../services';

export interface IUseTransferResult {
  transfer: Transfer | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  isPolling: boolean;
}

const pollingIntervalInMilliseconds = 2000;

const maximumTimerDelayInMilliseconds = 2_147_483_647;

function millisecondsUntilDue(transfer: Transfer): number | null {
  if (transfer.status !== 'Scheduled' || transfer.scheduledFor === null) {
    return null;
  }

  return new Date(transfer.scheduledFor).getTime() - Date.now();
}

function shouldPoll(transfer: Transfer | undefined): boolean {
  if (!transfer) {
    return false;
  }

  if (transfer.status === 'Processing') {
    return true;
  }

  const remainingMilliseconds = millisecondsUntilDue(transfer);
  return remainingMilliseconds !== null && remainingMilliseconds <= 0;
}

function findRefetchInterval(transfer: Transfer | undefined): number | false {
  if (!transfer) {
    return false;
  }

  if (shouldPoll(transfer)) {
    return pollingIntervalInMilliseconds;
  }

  const remainingMilliseconds = millisecondsUntilDue(transfer);

  if (remainingMilliseconds === null) {
    return false;
  }

  return Math.min(
    Math.max(remainingMilliseconds, pollingIntervalInMilliseconds),
    maximumTimerDelayInMilliseconds
  );
}

export function useTransfer(transferId: string): IUseTransferResult {
  const {
    data: transfer,
    isPending,
    isError,
    error,
  } = useQuery({
    queryKey: queryKeys.transfers.detail(transferId),
    queryFn: () => transfersService.findById(transferId),
    refetchInterval: (query) => findRefetchInterval(query.state.data),
  });

  return {
    transfer,
    isPending,
    isError,
    error,
    isPolling: shouldPoll(transfer),
  };
}
