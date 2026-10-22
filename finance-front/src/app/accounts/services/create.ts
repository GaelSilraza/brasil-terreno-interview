import { httpClient } from '@src/shared/clients/httpClient';
import type { CreateAccountDTO } from '@src/shared/dto';
import type { Account } from '@src/shared/entities';

export async function create(account: CreateAccountDTO): Promise<Account> {
  const { data } = await httpClient.post<Account>('/accounts', account);

  return data;
}
