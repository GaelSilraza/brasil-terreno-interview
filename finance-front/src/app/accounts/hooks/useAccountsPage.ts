import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { Account } from '@src/shared/entities';
import type { ICursorState } from '@src/shared/hooks/useCursorPagination';

import { accountsService } from '../services';

export interface IUseAccountsPageResult {
  accounts: Account[];
  nextCursor: string | null;
  previousCursor: string | null;
  isPending: boolean;
  isFetching: boolean;
  isError: boolean;
}

export const accountsPageLimit = 20;

const noAccounts: Account[] = [];

export function useAccountsPage(
  search: string,
  cursorState: ICursorState
): IUseAccountsPageResult {
  const { cursor, direction } = cursorState;
  const { data, isPending, isFetching, isError } = useQuery({
    queryKey: queryKeys.accounts.page(search, cursor, direction),
    queryFn: () =>
      accountsService.findAll({
        search,
        cursor,
        direction,
        limit: accountsPageLimit,
      }),
    placeholderData: keepPreviousData,
  });

  return {
    accounts: data?.items ?? noAccounts,
    nextCursor: data?.nextCursor ?? null,
    previousCursor: data?.previousCursor ?? null,
    isPending,
    isFetching,
    isError,
  };
}
