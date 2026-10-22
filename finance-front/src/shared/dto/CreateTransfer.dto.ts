export interface CreateTransferDTO {
  sourceAccountId: string;
  destinationAccountId: string;
  amount: number;
  idempotencyKey: string;
}
