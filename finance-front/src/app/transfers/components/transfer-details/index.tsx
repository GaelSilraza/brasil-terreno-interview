import type { JSX } from 'react';
import { getRouteApi } from '@tanstack/react-router';

import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { PageHeader } from '@src/shared/components/PageHeader';
import { Skeleton } from '@src/shared/components/ui/Skeleton';
import { getApiErrorMessage, isNotFoundProblem } from '@src/shared/utils';

import { useTransfer } from '../../hooks/useTransfer';
import { CancelTransferButton } from './CancelTransferButton';
import { TransferDetailsCard } from './TransferDetailsCard';

const transferDetailsRoute = getRouteApi('/transfers/$transferId');

function findLoadErrorTitle(error: unknown): string {
  return isNotFoundProblem(error)
    ? 'Transferência não encontrada'
    : 'Não foi possível carregar a transferência';
}

export function TransferDetails(): JSX.Element {
  const { transferId } = transferDetailsRoute.useParams();
  const { transfer, isPending, isError, error, isPolling } =
    useTransfer(transferId);

  return (
    <>
      <PageHeader
        title='Detalhes da transferência'
        actions={
          transfer?.status === 'Scheduled' && (
            <CancelTransferButton transfer={transfer} />
          )
        }
      />
      {isPending && <Skeleton className='h-64 w-full' />}
      {isError && !transfer && (
        <ApiErrorAlert
          title={findLoadErrorTitle(error)}
          message={getApiErrorMessage(error)}
        />
      )}
      {transfer && (
        <TransferDetailsCard transfer={transfer} isPolling={isPolling} />
      )}
    </>
  );
}
