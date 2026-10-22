import type { JSX, ReactNode } from 'react';
import {
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type PaginationState,
  type Table,
} from '@tanstack/react-table';

import { DataTableContext } from '@src/shared/contexts/DataTableContext';

interface IDataTableProps<TData> {
  columns: ColumnDef<TData>[];
  data: TData[];
  children: ReactNode;
  pagination?: PaginationState;
}

export function DataTable<TData>({
  columns,
  data,
  children,
  pagination,
}: IDataTableProps<TData>): JSX.Element {
  const table = useReactTable({
    data,
    columns,
    columnResizeMode: 'onChange',
    enableColumnResizing: true,
    initialState: {
      pagination,
      columnSizing: {},
      columnVisibility: {},
    },
    defaultColumn: {
      minSize: 30,
    },
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
  });

  return (
    <DataTableContext.Provider
      value={{ table: table as unknown as Table<unknown> }}
    >
      {children}
    </DataTableContext.Provider>
  );
}
