import { useCallback, useState, type JSX } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ChevronsUpDown } from 'lucide-react';

import { Button } from '@src/shared/components/ui/Button';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@src/shared/components/ui/Popover';
import { queryKeys } from '@src/shared/configs/queryKeys';
import type { Account } from '@src/shared/entities';
import { cn } from '@src/shared/lib/utils';

import { useAccount } from '../hooks/useAccount';
import { AccountComboboxOptions } from './AccountComboboxOptions';

export interface IAccountComboboxProps {
  triggerId: string;
  value: string;
  onChange: (accountId: string) => void;
  onBlur: () => void;
  placeholder: string;
  excludedAccountId?: string;
  errorId?: string;
}

export function AccountCombobox({
  triggerId,
  value,
  onChange,
  onBlur,
  placeholder,
  excludedAccountId,
  errorId,
}: IAccountComboboxProps): JSX.Element {
  const [isOpen, setIsOpen] = useState<boolean>(false);
  const queryClient = useQueryClient();
  const { account } = useAccount(value);
  const selectedLabel = account?.holderName;

  const handleSelect = useCallback(
    (chosenAccount: Account): void => {
      const detailQueryKey = queryKeys.accounts.detail(chosenAccount.id);

      if (queryClient.getQueryData(detailQueryKey) === undefined) {
        queryClient.setQueryData(detailQueryKey, chosenAccount);
      }
      onChange(chosenAccount.id);
      setIsOpen(false);
    },
    [onChange, queryClient]
  );

  const handleOpenChange = useCallback(
    (nextIsOpen: boolean): void => {
      setIsOpen(nextIsOpen);

      if (!nextIsOpen) {
        onBlur();
      }
    },
    [onBlur]
  );

  return (
    <Popover open={isOpen} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          id={triggerId}
          type='button'
          role='combobox'
          aria-expanded={isOpen}
          aria-invalid={errorId !== undefined}
          aria-describedby={errorId}
          variant='outline'
          className={cn(
            'w-full justify-between font-normal',
            selectedLabel === undefined && 'text-muted-foreground'
          )}
        >
          <span className='min-w-0 truncate'>
            {selectedLabel ?? placeholder}
          </span>
          <ChevronsUpDown className='opacity-50' />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        align='start'
        className='w-[var(--radix-popover-trigger-width)] max-w-[var(--radix-popover-content-available-width)] p-0'
      >
        <AccountComboboxOptions
          selectedAccountId={value}
          excludedAccountId={excludedAccountId}
          onSelect={handleSelect}
        />
      </PopoverContent>
    </Popover>
  );
}
