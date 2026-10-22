import { flexRender } from '@tanstack/react-table';

import {
  TableHeader,
  TableRow,
  TableHead,
} from '@src/shared/components/ui/Table';
import { cn } from '@src/shared/lib/utils';
import { useDataTable } from '@src/shared/hooks/useDataTable';

export function DataTableHeader() {
  const { table } = useDataTable();

  return (
    <TableHeader className='bg-muted'>
      {table.getHeaderGroups().map((headerGroup) => (
        <TableRow key={headerGroup.id}>
          {headerGroup.headers.map((header) => (
            <TableHead
              key={header.id}
              style={{
                width: `calc(var(--header-${header.id}-size) * 1px)`,
              }}
              className='relative group truncate'
            >
              {!header.isPlaceholder &&
                flexRender(header.column.columnDef.header, header.getContext())}
              {header.column.getCanResize() && (
                <div
                  className={cn(
                    'absolute right-0 h-full bg-primary w-1.5 top-0 cursor-col-resize opacity-0 group-hover:opacity-100 transition-all duration-300',
                    header.column.getIsResizing() && 'opacity-100'
                  )}
                  onMouseDown={header.getResizeHandler()}
                  onTouchStart={header.getResizeHandler()}
                ></div>
              )}
            </TableHead>
          ))}
        </TableRow>
      ))}
    </TableHeader>
  );
}
