import type { ColumnDef } from '@tanstack/react-table';

import { AccountStatusBadge } from '@src/shared/components/AccountStatusBadge';
import type { Account } from '@src/shared/entities';
import { cn } from '@src/shared/lib/utils';
import { formatCurrency, formatDocumentNumber } from '@src/shared/utils';

import { ActionsMenu } from './ActionsMenu';

export const Columns: ColumnDef<Account>[] = [
  {
    accessorKey: 'holderName',
    meta: { nameInFilters: 'Titular' },
    header: 'Titular',
  },
  {
    accessorKey: 'documentNumber',
    meta: { nameInFilters: 'CPF' },
    header: 'CPF',
    cell: ({ row }) => formatDocumentNumber(row.original.documentNumber),
  },
  {
    accessorKey: 'balance',
    meta: { nameInFilters: 'Saldo' },
    header: 'Saldo',
    cell: ({ row }) => (
      <span className={cn(row.original.balance < 0 && 'text-destructive')}>
        {formatCurrency(row.original.balance)}
      </span>
    ),
  },
  {
    accessorKey: 'overdraftLimit',
    meta: { nameInFilters: 'Cheque especial' },
    header: 'Cheque especial',
    cell: ({ row }) => formatCurrency(row.original.overdraftLimit),
  },
  {
    accessorKey: 'availableAmount',
    meta: { nameInFilters: 'Disponível' },
    header: 'Disponível',
    cell: ({ row }) => formatCurrency(row.original.availableAmount),
  },
  {
    accessorKey: 'status',
    meta: { nameInFilters: 'Status' },
    header: 'Status',
    cell: ({ row }) => <AccountStatusBadge status={row.original.status} />,
  },
  {
    id: 'actions',
    header: '',
    size: 40,
    enableColumnFilter: false,
    enableGlobalFilter: false,
    enableHiding: false,
    enableSorting: false,
    enableResizing: false,
    cell: ({ row }) => <ActionsMenu account={row.original} />,
  },
];
