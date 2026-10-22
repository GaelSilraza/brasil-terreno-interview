import type { JSX } from 'react';

import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { CursorPaginationControls } from '@src/shared/components/CursorPaginationControls';
import {
  DataTable,
  DataTableColumnsVisibilityDropdown,
  DataTableContent,
} from '@src/shared/components/ui/DataTable';
import { Spinner } from '@src/shared/components/ui/Spinner';
import { useCursorPagination } from '@src/shared/hooks/useCursorPagination';
import { defaultApiErrorMessage } from '@src/shared/resources';

import { accountsPageLimit, useAccountsPage } from '../hooks/useAccountsPage';
import { Columns } from './Columns';

export interface IAccountsTableResultsProps {
  search: string;
}

const tablePagination = { pageIndex: 0, pageSize: accountsPageLimit };

export function AccountsTableResults({
  search,
}: IAccountsTableResultsProps): JSX.Element {
  const { cursorState, goToNextPage, goToPreviousPage } = useCursorPagination();
  const {
    accounts,
    nextCursor,
    previousCursor,
    isPending,
    isFetching,
    isError,
  } = useAccountsPage(search, cursorState);

  if (isError) {
    return (
      <ApiErrorAlert
        title='Erro ao carregar contas'
        message={defaultApiErrorMessage}
      />
    );
  }

  if (isPending) {
    return (
      <div className='flex justify-center py-10'>
        <Spinner className='size-8 text-primary' />
      </div>
    );
  }

  return (
    <DataTable columns={Columns} data={accounts} pagination={tablePagination}>
      <div className='flex justify-end'>
        <DataTableColumnsVisibilityDropdown />
      </div>
      <DataTableContent />
      {accounts.length === 0 && (
        <p className='text-center text-sm text-muted-foreground'>
          {search === ''
            ? 'Nenhuma conta encontrada.'
            : `Nenhuma conta encontrada para "${search}".`}
        </p>
      )}
      <CursorPaginationControls
        hasPrevious={previousCursor !== null}
        hasNext={nextCursor !== null}
        isFetching={isFetching}
        onPrevious={() => goToPreviousPage(previousCursor)}
        onNext={() => goToNextPage(nextCursor)}
      />
    </DataTable>
  );
}
