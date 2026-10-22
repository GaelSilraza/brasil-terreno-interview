import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { Transfer } from '@src/shared/entities';
import type { ICursorState } from '@src/shared/hooks/useCursorPagination';

import { transfersService } from '../services';

export interface IUseAccountTransfersResult {
  transfers: Transfer[];
  nextCursor: string | null;
  previousCursor: string | null;
  isPending: boolean;
  isFetching: boolean;
  isError: boolean;
}

const pageLimit = 10;

const noTransfers: Transfer[] = [];

export function useAccountTransfers(
  accountId: string,
  { cursor, direction }: ICursorState
): IUseAccountTransfersResult {
  const { data, isPending, isFetching, isError } = useQuery({
    queryKey: queryKeys.transfers.byAccountPage(accountId, cursor, direction),
    queryFn: () =>
      transfersService.findByAccount(accountId, {
        cursor,
        direction,
        limit: pageLimit,
      }),
    placeholderData: keepPreviousData,
  });

  return {
    transfers: data?.items ?? noTransfers,
    nextCursor: data?.nextCursor ?? null,
    previousCursor: data?.previousCursor ?? null,
    isPending,
    isFetching,
    isError,
  };
}
