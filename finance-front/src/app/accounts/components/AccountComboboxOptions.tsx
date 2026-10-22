import { useState, type JSX, type UIEvent } from 'react';
import { Check } from 'lucide-react';

import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@src/shared/components/ui/Command';
import type { Account } from '@src/shared/entities';
import { useDebouncedValue } from '@src/shared/hooks/useDebouncedValue';
import { cn } from '@src/shared/lib/utils';
import { accountStatusLabels } from '@src/shared/resources';
import { formatCurrency, formatDocumentNumber } from '@src/shared/utils';

import { useAccountSearch } from '../hooks/useAccountSearch';
import { maximumAccountSearchLength } from '../hooks/useAccountsSearchForm';

export interface IAccountComboboxOptionsProps {
  selectedAccountId: string;
  excludedAccountId?: string;
  onSelect: (account: Account) => void;
}

const loadMoreThresholdInPixels = 48;

function formatAccountOptionLabel(account: Account): string {
  const accountDescription = `${account.holderName} · ${formatDocumentNumber(account.documentNumber)}`;

  return account.status === 'Active'
    ? `${accountDescription} · ${formatCurrency(account.availableAmount)} disponível`
    : `${accountDescription} · ${accountStatusLabels[account.status]}`;
}

function findEmptyMessage(isFetching: boolean, isError: boolean): string {
  if (isFetching) {
    return 'Buscando contas...';
  }

  return isError
    ? 'Não foi possível buscar as contas.'
    : 'Nenhuma conta encontrada.';
}

export function AccountComboboxOptions({
  selectedAccountId,
  excludedAccountId,
  onSelect,
}: IAccountComboboxOptionsProps): JSX.Element {
  const [searchText, setSearchText] = useState<string>('');
  const debouncedSearch = useDebouncedValue(searchText.trim());
  const {
    accounts,
    hasNextPage,
    fetchNextPage,
    isFetching,
    isFetchingNextPage,
    isError,
  } = useAccountSearch(debouncedSearch);
  const selectableAccounts = accounts.filter(
    (account) => account.id !== excludedAccountId
  );

  function loadMoreWhenNearTheEnd(event: UIEvent<HTMLDivElement>): void {
    const list = event.currentTarget;
    const distanceToTheEnd =
      list.scrollHeight - list.scrollTop - list.clientHeight;

    if (
      distanceToTheEnd <= loadMoreThresholdInPixels &&
      hasNextPage &&
      !isFetchingNextPage
    ) {
      fetchNextPage();
    }
  }

  return (
    <Command shouldFilter={false} label='Buscar conta'>
      <CommandInput
        placeholder='Buscar por nome ou CPF...'
        maxLength={maximumAccountSearchLength}
        value={searchText}
        onValueChange={setSearchText}
      />
      <CommandList label='Contas' onScroll={loadMoreWhenNearTheEnd}>
        <CommandEmpty>{findEmptyMessage(isFetching, isError)}</CommandEmpty>
        <CommandGroup>
          {selectableAccounts.map((account) => (
            <CommandItem
              key={account.id}
              value={account.id}
              onSelect={() => onSelect(account)}
            >
              <span className='min-w-0 flex-1 truncate'>
                {formatAccountOptionLabel(account)}
              </span>
              <Check
                className={cn(
                  'ml-auto shrink-0',
                  account.id === selectedAccountId ? 'opacity-100' : 'opacity-0'
                )}
              />
            </CommandItem>
          ))}
        </CommandGroup>
        {hasNextPage && (
          <CommandItem
            value='load-more-accounts'
            disabled={isFetchingNextPage}
            onSelect={fetchNextPage}
          >
            {isFetchingNextPage
              ? 'Carregando contas...'
              : 'Carregar mais contas'}
          </CommandItem>
        )}
      </CommandList>
    </Command>
  );
}
