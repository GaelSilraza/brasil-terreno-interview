import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { AccountEntry } from '@src/shared/entities';
import type { ICursorState } from '@src/shared/hooks/useCursorPagination';

import { accountsService } from '../services';

export interface IUseAccountEntriesResult {
  entries: AccountEntry[];
  nextCursor: string | null;
  previousCursor: string | null;
  isPending: boolean;
  isFetching: boolean;
  isError: boolean;
}

const accountEntriesLimit = 10;

const noEntries: AccountEntry[] = [];

export function useAccountEntries(
  accountId: string,
  cursorState: ICursorState
): IUseAccountEntriesResult {
  const { cursor, direction } = cursorState;
  const { data, isPending, isFetching, isError } = useQuery({
    queryKey: queryKeys.accounts.entriesPage(accountId, cursor, direction),
    queryFn: () =>
      accountsService.findEntries(accountId, {
        cursor,
        direction,
        limit: accountEntriesLimit,
      }),
    placeholderData: keepPreviousData,
  });

  return {
    entries: data?.items ?? noEntries,
    nextCursor: data?.nextCursor ?? null,
    previousCursor: data?.previousCursor ?? null,
    isPending,
    isFetching,
    isError,
  };
}
