import { httpClient } from '@src/shared/clients/httpClient';
import type { UpdateAccountStatusDTO } from '@src/shared/dto';

export async function updateStatus({
  accountId,
  status,
}: UpdateAccountStatusDTO): Promise<void> {
  await httpClient.put(`/accounts/${accountId}/status`, { status });
}
