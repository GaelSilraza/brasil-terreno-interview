import { createContext } from 'react';
import type { Table } from '@tanstack/react-table';

export interface IDataTableContext {
  table: Table<unknown>;
}

export const DataTableContext = createContext<IDataTableContext | null>(null);
