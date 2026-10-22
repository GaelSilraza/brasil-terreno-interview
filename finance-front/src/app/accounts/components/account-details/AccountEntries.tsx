import type { JSX } from 'react';

import { CursorPaginationControls } from '@src/shared/components/CursorPaginationControls';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@src/shared/components/ui/card';
import { Spinner } from '@src/shared/components/ui/Spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@src/shared/components/ui/Table';
import type { AccountEntry } from '@src/shared/entities';
import { useCursorPagination } from '@src/shared/hooks/useCursorPagination';
import { cn } from '@src/shared/lib/utils';
import { accountEntryTypeLabels } from '@src/shared/resources';
import { formatCurrency, formatDateTime } from '@src/shared/utils';

import { useAccountEntries } from '../../hooks/useAccountEntries';

export interface IAccountEntriesProps {
  accountId: string;
}

function formatSignedAmount(entry: AccountEntry): string {
  const sign = entry.entryType === 'Debit' ? '-' : '+';
  return `${sign} ${formatCurrency(entry.amount)}`;
}

export function AccountEntries({
  accountId,
}: IAccountEntriesProps): JSX.Element {
  const { cursorState, goToNextPage, goToPreviousPage } = useCursorPagination();
  const {
    entries,
    nextCursor,
    previousCursor,
    isPending,
    isFetching,
    isError,
  } = useAccountEntries(accountId, cursorState);

  return (
    <section aria-label='Extrato'>
      <Card>
        <CardHeader>
          <CardTitle>Extrato</CardTitle>
        </CardHeader>
        <CardContent className='space-y-4'>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Data</TableHead>
                <TableHead>Tipo</TableHead>
                <TableHead>Contraparte</TableHead>
                <TableHead>Valor</TableHead>
                <TableHead>Saldo após</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isPending && (
                <TableRow>
                  <TableCell colSpan={5} className='text-center'>
                    <Spinner className='mx-auto' />
                  </TableCell>
                </TableRow>
              )}
              {isError && (
                <TableRow>
                  <TableCell
                    colSpan={5}
                    className='text-center text-destructive'
                  >
                    Não foi possível carregar o extrato.
                  </TableCell>
                </TableRow>
              )}
              {!isPending && !isError && entries.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={5}
                    className='text-center text-muted-foreground'
                  >
                    Nenhuma movimentação registrada.
                  </TableCell>
                </TableRow>
              )}
              {entries.map((entry) => (
                <TableRow key={entry.id}>
                  <TableCell>{formatDateTime(entry.createdAt)}</TableCell>
                  <TableCell>
                    {accountEntryTypeLabels[entry.entryType]}
                  </TableCell>
                  <TableCell>
                    {entry.counterpartHolderName ?? 'Saldo inicial'}
                  </TableCell>
                  <TableCell
                    className={cn(
                      entry.entryType === 'Debit'
                        ? 'text-destructive'
                        : 'text-emerald-600'
                    )}
                  >
                    {formatSignedAmount(entry)}
                  </TableCell>
                  <TableCell
                    className={cn(entry.balanceAfter < 0 && 'text-destructive')}
                  >
                    {formatCurrency(entry.balanceAfter)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <CursorPaginationControls
            hasPrevious={previousCursor !== null}
            hasNext={nextCursor !== null}
            isFetching={isFetching}
            onPrevious={() => goToPreviousPage(previousCursor)}
            onNext={() => goToNextPage(nextCursor)}
          />
        </CardContent>
      </Card>
    </section>
  );
}
