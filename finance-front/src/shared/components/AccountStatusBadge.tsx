import type { JSX } from 'react';

import { Badge } from '@src/shared/components/ui/badge';
import { accountStatusLabels } from '@src/shared/resources';
import type { AccountStatus } from '@src/shared/types';

export interface IAccountStatusBadgeProps {
  status: AccountStatus;
}

type BadgeVariant = 'default' | 'secondary' | 'destructive';

const accountStatusVariants: Record<AccountStatus, BadgeVariant> = {
  Active: 'default',
  Blocked: 'destructive',
  Inactive: 'secondary',
};

export function AccountStatusBadge({
  status,
}: IAccountStatusBadgeProps): JSX.Element {
  return (
    <Badge
      variant={accountStatusVariants[status]}
      className={status === 'Active' ? 'bg-emerald-600 text-white' : undefined}
    >
      {accountStatusLabels[status]}
    </Badge>
  );
}
