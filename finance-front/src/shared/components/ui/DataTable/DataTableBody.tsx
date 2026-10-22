import { memo } from 'react';
import { flexRender } from '@tanstack/react-table';

import {
  TableBody,
  TableCell,
  TableRow,
} from '@src/shared/components/ui/Table';
import { useDataTable } from '@src/shared/hooks/useDataTable';

export function DataTableBody() {
  const { table } = useDataTable();

  return (
    <TableBody>
      {table.getRowModel().rows.map((row) => (
        <TableRow key={row.id} data-state={row.getIsSelected() && 'selected'}>
          {row.getVisibleCells().map((cell) => (
            <TableCell
              key={cell.id}
              className='truncate'
              style={{
                width: `calc(var(--header-${cell.column.id}-size) * 1px)`,
              }}
            >
              {flexRender(cell.column.columnDef.cell, cell.getContext())}
            </TableCell>
          ))}
        </TableRow>
      ))}
    </TableBody>
  );
}

export const MemoizedDataTableBody = memo(DataTableBody);
