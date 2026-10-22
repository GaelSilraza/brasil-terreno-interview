import { httpClient } from '@src/shared/clients/httpClient';
import type { CursorQueryDTO } from '@src/shared/dto';
import type { CursorPage, Transfer } from '@src/shared/entities';

export async function findByAccount(
  accountId: string,
  { cursor, direction, limit }: CursorQueryDTO
): Promise<CursorPage<Transfer>> {
  const { data } = await httpClient.get<CursorPage<Transfer>>('/transfers', {
    params: { accountId, cursor, direction, limit },
  });

  return data;
}
