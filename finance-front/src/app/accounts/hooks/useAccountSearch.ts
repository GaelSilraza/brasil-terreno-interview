import { useCallback, useMemo } from 'react';
import { useInfiniteQuery } from '@tanstack/react-query';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { Account } from '@src/shared/entities';

import { accountsService } from '../services';

export interface IUseAccountSearchResult {
  accounts: Account[];
  hasNextPage: boolean;
  fetchNextPage: () => void;
  isFetching: boolean;
  isFetchingNextPage: boolean;
  isError: boolean;
}

const accountSearchLimit = 20;

const noAccounts: Account[] = [];

export function useAccountSearch(search: string): IUseAccountSearchResult {
  const {
    data,
    hasNextPage,
    fetchNextPage: fetchNextQueryPage,
    isFetching,
    isFetchingNextPage,
    isError,
  } = useInfiniteQuery({
    queryKey: queryKeys.accounts.search(search),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      accountsService.findAll({
        search,
        cursor: pageParam,
        direction: 'next',
        limit: accountSearchLimit,
      }),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });

  const accounts = useMemo(
    (): Account[] =>
      data ? data.pages.flatMap((page) => page.items) : noAccounts,
    [data]
  );

  const fetchNextPage = useCallback((): void => {
    void fetchNextQueryPage();
  }, [fetchNextQueryPage]);

  return {
    accounts,
    hasNextPage,
    fetchNextPage,
    isFetching,
    isFetchingNextPage,
    isError,
  };
}
