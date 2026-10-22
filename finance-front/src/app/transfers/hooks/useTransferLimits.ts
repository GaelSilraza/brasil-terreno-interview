import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { TransferLimits } from '@src/shared/entities';

import { transfersService } from '../services';

export interface IUseTransferLimitsResult {
  limits: TransferLimits | undefined;
  isPending: boolean;
  isError: boolean;
}

export function useTransferLimits(accountId: string): IUseTransferLimitsResult {
  const {
    data: limits,
    isPending,
    isError,
  } = useQuery({
    queryKey: queryKeys.transfers.limits(accountId),
    queryFn: () => transfersService.findLimits(accountId),
  });

  return { limits, isPending, isError };
}
