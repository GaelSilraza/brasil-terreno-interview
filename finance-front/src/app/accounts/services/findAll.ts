import { httpClient } from '@src/shared/clients/httpClient';
import type { AccountSearchQueryDTO } from '@src/shared/dto';
import type { Account, CursorPage } from '@src/shared/entities';

export async function findAll({
  search,
  cursor,
  direction,
  limit,
}: AccountSearchQueryDTO): Promise<CursorPage<Account>> {
  const { data } = await httpClient.get<CursorPage<Account>>('/accounts', {
    params: {
      limit,
      ...(search ? { search } : {}),
      ...(cursor ? { cursor } : {}),
      ...(direction ? { direction } : {}),
    },
  });

  return data;
}
