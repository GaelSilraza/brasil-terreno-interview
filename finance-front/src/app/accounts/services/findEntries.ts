import { httpClient } from '@src/shared/clients/httpClient';
import type { CursorQueryDTO } from '@src/shared/dto';
import type { AccountEntry, CursorPage } from '@src/shared/entities';

export async function findEntries(
  accountId: string,
  { cursor, direction, limit }: CursorQueryDTO
): Promise<CursorPage<AccountEntry>> {
  const { data } = await httpClient.get<CursorPage<AccountEntry>>(
    `/accounts/${accountId}/entries`,
    {
      params: {
        limit,
        ...(cursor ? { cursor } : {}),
        ...(direction ? { direction } : {}),
      },
    }
  );

  return data;
}
