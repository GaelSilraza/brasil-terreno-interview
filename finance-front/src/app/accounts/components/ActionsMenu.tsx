import type { JSX } from 'react';
import { Link } from '@tanstack/react-router';
import { ArrowRightLeft, Eye, MoreHorizontal } from 'lucide-react';

import { Button } from '@src/shared/components/ui/Button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@src/shared/components/ui/DropdownMenu';
import type { Account } from '@src/shared/entities';

export interface IActionsMenuProps {
  account: Account;
}

export function ActionsMenu({ account }: IActionsMenuProps): JSX.Element {
  return (
    <div className='flex justify-end'>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant='ghost'
            size='icon'
            className='cursor-pointer'
            aria-label={`Ações de ${account.holderName}`}
          >
            <MoreHorizontal />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align='end'>
          <DropdownMenuItem asChild>
            <Link to='/accounts/$accountId' params={{ accountId: account.id }}>
              <Eye className='size-4' />
              Ver detalhes
            </Link>
          </DropdownMenuItem>
          <DropdownMenuItem asChild>
            <Link
              to='/transfers'
              search={{ tab: 'immediate', sourceAccountId: account.id }}
            >
              <ArrowRightLeft className='size-4' />
              Nova transferência
            </Link>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
