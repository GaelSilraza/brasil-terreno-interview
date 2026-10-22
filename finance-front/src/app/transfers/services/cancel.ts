import { httpClient } from '@src/shared/clients/httpClient';
import type { Transfer } from '@src/shared/entities';

export async function cancel(transferId: string): Promise<Transfer> {
  const { data } = await httpClient.post<Transfer>(
    `/transfers/${transferId}/cancel`
  );

  return data;
}
