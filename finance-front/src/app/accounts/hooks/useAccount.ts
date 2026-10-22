import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { Account } from '@src/shared/entities';

import { accountsService } from '../services';

export interface IUseAccountResult {
  account: Account | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
}

export function useAccount(accountId: string): IUseAccountResult {
  const { data, isPending, isError, error } = useQuery({
    queryKey: queryKeys.accounts.detail(accountId),
    queryFn: () => accountsService.findById(accountId),
    enabled: accountId !== '',
  });

  return { account: data, isPending, isError, error };
}
