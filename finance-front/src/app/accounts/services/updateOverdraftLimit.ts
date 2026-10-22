import { httpClient } from '@src/shared/clients/httpClient';
import type { UpdateOverdraftLimitDTO } from '@src/shared/dto';

export async function updateOverdraftLimit({
  accountId,
  overdraftLimit,
}: UpdateOverdraftLimitDTO): Promise<void> {
  await httpClient.put(`/accounts/${accountId}/overdraft-limit`, {
    overdraftLimit,
  });
}
