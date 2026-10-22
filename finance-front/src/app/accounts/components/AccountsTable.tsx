import type { JSX } from 'react';
import { useWatch, type Control } from 'react-hook-form';

import { useDebouncedValue } from '@src/shared/hooks/useDebouncedValue';

import type { AccountsSearchFormValues } from '../hooks/useAccountsSearchForm';
import { AccountsTableResults } from './AccountsTableResults';

export interface IAccountsTableProps {
  control: Control<AccountsSearchFormValues>;
}

export function AccountsTable({ control }: IAccountsTableProps): JSX.Element {
  const search = useWatch({ control, name: 'search' });
  const debouncedSearch = useDebouncedValue(search.trim());

  return (
    <AccountsTableResults key={debouncedSearch} search={debouncedSearch} />
  );
}
