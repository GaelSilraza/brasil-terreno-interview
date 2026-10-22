import { useMemo } from 'react';

import { Table } from '@src/shared/components/ui/Table';
import { MemoizedDataTableBody, DataTableBody } from './DataTableBody';
import { DataTableHeader } from './DataTableHeader';
import { useDataTable } from '@src/shared/hooks/useDataTable';

export function DataTableContent() {
  const { table } = useDataTable();

  const { columnSizingInfo, columnSizing } = table.getState();

  const colSizeVariables = useMemo(() => {
    const variables: Record<string, number> = {};

    table.getFlatHeaders().map((header) => {
      variables[`--header-${header.id}-size`] = header.getSize();
      variables[`--col-${header.column.id}-size`] = header.getSize();
    });

    return variables;
  }, [columnSizingInfo, columnSizing, table]);

  return (
    <div className='border rounded-md'>
      <Table style={colSizeVariables}>
        <DataTableHeader />

        {columnSizingInfo.isResizingColumn && <MemoizedDataTableBody />}
        {!columnSizingInfo.isResizingColumn && <DataTableBody />}
      </Table>
    </div>
  );
}
