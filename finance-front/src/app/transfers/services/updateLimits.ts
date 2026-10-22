import { httpClient } from '@src/shared/clients/httpClient';
import type { SetTransferLimitsDTO } from '@src/shared/dto';

export async function updateLimits({
  accountId,
  ...limits
}: SetTransferLimitsDTO): Promise<void> {
  await httpClient.put(`/transfer-limits/${accountId}`, limits);
}
