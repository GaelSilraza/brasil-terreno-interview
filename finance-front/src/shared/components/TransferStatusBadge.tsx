import type { JSX } from 'react';
import { Loader2Icon } from 'lucide-react';

import { Badge } from '@src/shared/components/ui/badge';
import { transferStatusLabels } from '@src/shared/resources';
import type { TransferStatus } from '@src/shared/types';

export interface ITransferStatusBadgeProps {
  status: TransferStatus;
}

type BadgeVariant = 'default' | 'secondary' | 'destructive' | 'outline';

const transferStatusVariants: Record<TransferStatus, BadgeVariant> = {
  Scheduled: 'secondary',
  Processing: 'outline',
  Completed: 'default',
  Failed: 'destructive',
  Cancelled: 'outline',
};

export function TransferStatusBadge({
  status,
}: ITransferStatusBadgeProps): JSX.Element {
  return (
    <Badge
      variant={transferStatusVariants[status]}
      className={
        status === 'Completed' ? 'bg-emerald-600 text-white' : undefined
      }
    >
      {status === 'Processing' && <Loader2Icon className='animate-spin' />}
      {transferStatusLabels[status]}
    </Badge>
  );
}
