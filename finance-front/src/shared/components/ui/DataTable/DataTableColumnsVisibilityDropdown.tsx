import { Button } from '@src/shared/components/ui/Button';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from '@src/shared/components/ui/DropdownMenu';
import { useDataTable } from '@src/shared/hooks/useDataTable';
import { Settings2Icon } from 'lucide-react';
import { Fragment } from 'react/jsx-runtime';

export function DataTableColumnsVisibilityDropdown() {
  const { table } = useDataTable();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button size='sm' variant='outline' className='cursor-pointer'>
          <Settings2Icon />
          Colunas
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align='end'>
        {table.getAllColumns().map((header) =>
          !header.getCanHide() ? (
            <Fragment key={header.id}></Fragment>
          ) : (
            <DropdownMenuCheckboxItem
              key={header.id}
              checked={header.getIsVisible()}
              onCheckedChange={header.toggleVisibility}
              disabled={!header.getCanHide()}
            >
              {header.columnDef.meta?.nameInFilters || header.id}
            </DropdownMenuCheckboxItem>
          )
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
