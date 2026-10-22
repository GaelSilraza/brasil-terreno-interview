export interface SetTransferLimitsDTO {
  accountId: string;
  daytimeMaxAmountPerHour: number;
  daytimeMaxAttemptsPerHour: number;
  nighttimeMaxAmountPerHour: number;
  nighttimeMaxAttemptsPerHour: number;
}
