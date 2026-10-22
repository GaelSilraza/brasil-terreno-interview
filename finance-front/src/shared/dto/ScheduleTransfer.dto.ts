import type { CreateTransferDTO } from './CreateTransfer.dto';

export interface ScheduleTransferDTO extends CreateTransferDTO {
  scheduledFor: string;
}
