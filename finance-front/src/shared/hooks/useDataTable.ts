import { useContext } from 'react';

import {
  DataTableContext,
  type IDataTableContext,
} from '@src/shared/contexts/DataTableContext';

export function useDataTable(): IDataTableContext {
  const contextValue = useContext(DataTableContext);

  if (!contextValue) {
    throw new Error('DataTable hook must be used inside a DataTableContext');
  }

  return contextValue;
}
