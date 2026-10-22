import type { JSX, ReactNode } from 'react';
import { Link } from '@tanstack/react-router';

import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { TransferStatusBadge } from '@src/shared/components/TransferStatusBadge';
import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
  CardTitle,
} from '@src/shared/components/ui/card';
import { Spinner } from '@src/shared/components/ui/Spinner';
import type { Transfer } from '@src/shared/entities';
import {
  apiErrorMessages,
  defaultApiErrorMessage,
  transferTypeLabels,
} from '@src/shared/resources';
import { formatCurrency, formatDateTime } from '@src/shared/utils';

export interface ITransferDetailsCardProps {
  transfer: Transfer;
  isPolling: boolean;
}

interface IDetailItemProps {
  label: string;
  children: ReactNode;
}

interface ITransferDate {
  label: string;
  value: string | null;
}

function DetailItem({ label, children }: IDetailItemProps): JSX.Element {
  return (
    <div className='grid gap-1'>
      <dt className='text-sm text-muted-foreground'>{label}</dt>
      <dd className='font-medium break-all'>{children}</dd>
    </div>
  );
}

function findFailureMessage(failureCode: string | null): string {
  if (!failureCode) {
    return defaultApiErrorMessage;
  }

  return apiErrorMessages[failureCode] ?? defaultApiErrorMessage;
}

export function TransferDetailsCard({
  transfer,
  isPolling,
}: ITransferDetailsCardProps): JSX.Element {
  const transferDates: ITransferDate[] = [
    { label: 'Agendada para', value: transfer.scheduledFor },
    { label: 'Tentativa em', value: transfer.attemptedAt },
    { label: 'Concluída em', value: transfer.completedAt },
    { label: 'Falhou em', value: transfer.failedAt },
    { label: 'Cancelada em', value: transfer.cancelledAt },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle>Transferência</CardTitle>
        <CardAction className='flex items-center gap-2'>
          {isPolling && (
            <span className='flex items-center gap-2 text-sm text-muted-foreground'>
              <Spinner aria-label='Atualizando' />
              Atualizando status...
            </span>
          )}
          <TransferStatusBadge status={transfer.status} />
        </CardAction>
      </CardHeader>
      <CardContent className='grid gap-6'>
        {transfer.status === 'Failed' && (
          <ApiErrorAlert
            title='Motivo da falha'
            message={findFailureMessage(transfer.failureCode)}
          />
        )}
        <dl className='grid gap-4 sm:grid-cols-2'>
          <DetailItem label='Valor'>
            {formatCurrency(transfer.amount)}
          </DetailItem>
          <DetailItem label='Tipo'>
            {transferTypeLabels[transfer.type]}
          </DetailItem>
          <DetailItem label='Origem'>
            <Link
              to='/accounts/$accountId'
              params={{ accountId: transfer.sourceAccountId }}
              className='text-primary underline-offset-4 hover:underline'
            >
              {transfer.sourceHolderName}
            </Link>
          </DetailItem>
          <DetailItem label='Destino'>
            <Link
              to='/accounts/$accountId'
              params={{ accountId: transfer.destinationAccountId }}
              className='text-primary underline-offset-4 hover:underline'
            >
              {transfer.destinationHolderName}
            </Link>
          </DetailItem>
          <DetailItem label='Código'>{transfer.id}</DetailItem>
          {transferDates.map(
            (transferDate) =>
              transferDate.value && (
                <DetailItem key={transferDate.label} label={transferDate.label}>
                  {formatDateTime(transferDate.value)}
                </DetailItem>
              )
          )}
        </dl>
      </CardContent>
    </Card>
  );
}
