import { useId, type JSX } from 'react';

import { Badge } from '@src/shared/components/ui/badge';
import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
  CardTitle,
} from '@src/shared/components/ui/card';
import { Skeleton } from '@src/shared/components/ui/Skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@src/shared/components/ui/Table';
import type { TransferLimits } from '@src/shared/entities';
import { transferPeriodLabels } from '@src/shared/resources';
import { formatCurrency } from '@src/shared/utils';

import { useTransferLimits } from '../hooks/useTransferLimits';
import { EditTransferLimitsModal } from './EditTransferLimitsModal';

export interface ITransferLimitsCardProps {
  accountId: string;
}

interface ITransferLimitsContentProps {
  limits: TransferLimits;
}

function TransferLimitsContent({
  limits,
}: ITransferLimitsContentProps): JSX.Element {
  const currentPeriodLimit =
    limits.currentPeriod === 'Daytime' ? limits.daytime : limits.nighttime;

  return (
    <div className='grid gap-4'>
      <p className='text-sm'>
        Período atual: {transferPeriodLabels[limits.currentPeriod]}
      </p>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Período</TableHead>
            <TableHead>Valor máximo</TableHead>
            <TableHead>Tentativas</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow>
            <TableCell>{transferPeriodLabels.Daytime}</TableCell>
            <TableCell>
              {formatCurrency(limits.daytime.maxAmountPerHour)}
            </TableCell>
            <TableCell>{limits.daytime.maxAttemptsPerHour}</TableCell>
          </TableRow>
          <TableRow>
            <TableCell>{transferPeriodLabels.Nighttime}</TableCell>
            <TableCell>
              {formatCurrency(limits.nighttime.maxAmountPerHour)}
            </TableCell>
            <TableCell>{limits.nighttime.maxAttemptsPerHour}</TableCell>
          </TableRow>
        </TableBody>
      </Table>
      <div className='grid gap-1 text-sm'>
        <p>
          Tentativas na última hora: {limits.usage.attemptsInLastHour} de{' '}
          {currentPeriodLimit.maxAttemptsPerHour}
        </p>
        <p>
          Valor transferido na última hora:{' '}
          {formatCurrency(limits.usage.completedAmountInLastHour)}
        </p>
        <p>Restante: {formatCurrency(limits.usage.remainingAmount)}</p>
      </div>
      <EditTransferLimitsModal limits={limits} />
    </div>
  );
}

export function TransferLimitsCard({
  accountId,
}: ITransferLimitsCardProps): JSX.Element {
  const titleId = useId();
  const { limits, isPending, isError } = useTransferLimits(accountId);

  return (
    <Card role='region' aria-labelledby={titleId}>
      <CardHeader>
        <CardTitle id={titleId}>Limites por hora</CardTitle>
        {limits && (
          <CardAction>
            <Badge variant={limits.isCustom ? 'default' : 'secondary'}>
              {limits.isCustom ? 'Personalizado' : 'Padrão'}
            </Badge>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        {isPending && <Skeleton className='h-32 w-full' />}
        {isError && (
          <p className='text-sm text-destructive'>
            Não foi possível carregar os limites.
          </p>
        )}
        {limits && <TransferLimitsContent limits={limits} />}
      </CardContent>
    </Card>
  );
}
