import { httpClient } from '@src/shared/clients/httpClient';
import type { TransferLimits } from '@src/shared/entities';

export async function findLimits(accountId: string): Promise<TransferLimits> {
  const { data } = await httpClient.get<TransferLimits>(
    `/transfer-limits/${accountId}`
  );

  return data;
}
