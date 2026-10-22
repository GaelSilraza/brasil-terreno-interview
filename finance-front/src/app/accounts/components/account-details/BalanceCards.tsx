import type { JSX } from 'react';

import { AccountStatusBadge } from '@src/shared/components/AccountStatusBadge';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@src/shared/components/ui/card';
import type { Account } from '@src/shared/entities';
import { cn } from '@src/shared/lib/utils';
import { formatCurrency } from '@src/shared/utils';

import { AccountStatusForm } from './AccountStatusForm';

export interface IBalanceCardsProps {
  account: Account;
}

export function BalanceCards({ account }: IBalanceCardsProps): JSX.Element {
  return (
    <div className='grid gap-4 sm:grid-cols-2 xl:grid-cols-4'>
      <Card>
        <CardHeader>
          <CardTitle>Saldo</CardTitle>
        </CardHeader>
        <CardContent>
          <p
            className={cn(
              'text-2xl font-semibold',
              account.balance < 0 && 'text-destructive'
            )}
          >
            {formatCurrency(account.balance)}
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Cheque especial</CardTitle>
        </CardHeader>
        <CardContent className='space-y-1'>
          <p className='text-2xl font-semibold'>
            {formatCurrency(account.overdraftLimit)}
          </p>
          <p className='text-sm text-muted-foreground'>
            {`Utilizado: ${formatCurrency(account.usedOverdraft)}`}
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Disponível para transferir</CardTitle>
        </CardHeader>
        <CardContent>
          <p className='text-2xl font-semibold'>
            {formatCurrency(account.availableAmount)}
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Status</CardTitle>
        </CardHeader>
        <CardContent className='space-y-4'>
          <AccountStatusBadge status={account.status} />
          <AccountStatusForm account={account} />
        </CardContent>
      </Card>
    </div>
  );
}
