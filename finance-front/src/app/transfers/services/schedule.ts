import { httpClient } from '@src/shared/clients/httpClient';
import type { ScheduleTransferDTO } from '@src/shared/dto';
import type { Transfer } from '@src/shared/entities';

export async function schedule({
  idempotencyKey,
  ...transfer
}: ScheduleTransferDTO): Promise<Transfer> {
  const { data } = await httpClient.post<Transfer>(
    '/transfers/scheduled',
    transfer,
    {
      headers: { 'Idempotency-Key': idempotencyKey },
    }
  );

  return data;
}
