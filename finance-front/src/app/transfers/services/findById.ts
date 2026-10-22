import { httpClient } from '@src/shared/clients/httpClient';
import type { Transfer } from '@src/shared/entities';

export async function findById(transferId: string): Promise<Transfer> {
  const { data } = await httpClient.get<Transfer>(`/transfers/${transferId}`);

  return data;
}
