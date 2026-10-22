import { httpClient } from '@src/shared/clients/httpClient';
import type { CreateTransferDTO } from '@src/shared/dto';
import type { Transfer } from '@src/shared/entities';

export async function create({
  idempotencyKey,
  ...transfer
}: CreateTransferDTO): Promise<Transfer> {
  const { data } = await httpClient.post<Transfer>('/transfers', transfer, {
    headers: { 'Idempotency-Key': idempotencyKey },
  });

  return data;
}
