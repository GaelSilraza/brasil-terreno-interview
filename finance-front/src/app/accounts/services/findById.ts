import { httpClient } from '@src/shared/clients/httpClient';
import type { Account } from '@src/shared/entities';

export async function findById(accountId: string): Promise<Account> {
  const { data } = await httpClient.get<Account>(`/accounts/${accountId}`);

  return data;
}
