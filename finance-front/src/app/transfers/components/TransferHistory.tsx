import { useId, type JSX } from 'react';
import { Link } from '@tanstack/react-router';

import { CursorPaginationControls } from '@src/shared/components/CursorPaginationControls';
import { TransferStatusBadge } from '@src/shared/components/TransferStatusBadge';
import {
  Card,
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
import type { Transfer } from '@src/shared/entities';
import { useCursorPagination } from '@src/shared/hooks/useCursorPagination';
import { transferTypeLabels } from '@src/shared/resources';
import { formatCurrency, formatDateTime } from '@src/shared/utils';

import { useAccountTransfers } from '../hooks/useAccountTransfers';

export interface ITransferHistoryProps {
  accountId: string;
}

function formatSignedAmount(transfer: Transfer, accountId: string): string {
  const sign = transfer.sourceAccountId === accountId ? '- ' : '+ ';
  return `${sign}${formatCurrency(transfer.amount)}`;
}

function TransferHistoryResults({
  accountId,
}: ITransferHistoryProps): JSX.Element {
  const { cursorState, goToNextPage, goToPreviousPage } = useCursorPagination();
  const {
    transfers,
    nextCursor,
    previousCursor,
    isPending,
    isFetching,
    isError,
  } = useAccountTransfers(accountId, cursorState);

  return (
    <>
      {isPending ? (
        <Skeleton className='h-32 w-full' />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Data</TableHead>
              <TableHead>Tipo</TableHead>
              <TableHead>Origem</TableHead>
              <TableHead>Destino</TableHead>
              <TableHead>Valor</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>
                <span className='sr-only'>Ações</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isError && (
              <TableRow>
                <TableCell
                  colSpan={7}
                  className='h-16 text-center text-destructive'
                >
                  Não foi possível carregar as transferências.
                </TableCell>
              </TableRow>
            )}
            {!isError && transfers.length === 0 && (
              <TableRow>
                <TableCell
                  colSpan={7}
                  className='h-16 text-center text-muted-foreground'
                >
                  Nenhuma transferência encontrada.
                </TableCell>
              </TableRow>
            )}
            {transfers.map((transfer) => (
              <TableRow key={transfer.id}>
                <TableCell>{formatDateTime(transfer.createdAt)}</TableCell>
                <TableCell>{transferTypeLabels[transfer.type]}</TableCell>
                <TableCell>{transfer.sourceHolderName}</TableCell>
                <TableCell>{transfer.destinationHolderName}</TableCell>
                <TableCell>{formatSignedAmount(transfer, accountId)}</TableCell>
                <TableCell>
                  <TransferStatusBadge status={transfer.status} />
                </TableCell>
                <TableCell>
                  <Link
                    to='/transfers/$transferId'
                    params={{ transferId: transfer.id }}
                    className='text-primary underline-offset-4 hover:underline'
                  >
                    Detalhes
                  </Link>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      <CursorPaginationControls
        hasPrevious={previousCursor !== null}
        hasNext={nextCursor !== null}
        isFetching={isFetching}
        onPrevious={() => goToPreviousPage(previousCursor)}
        onNext={() => goToNextPage(nextCursor)}
      />
    </>
  );
}

export function TransferHistory({
  accountId,
}: ITransferHistoryProps): JSX.Element {
  const titleId = useId();

  return (
    <Card role='region' aria-labelledby={titleId}>
      <CardHeader>
        <CardTitle id={titleId}>Transferências</CardTitle>
      </CardHeader>
      <CardContent className='grid gap-4'>
        <TransferHistoryResults key={accountId} accountId={accountId} />
      </CardContent>
    </Card>
  );
}
