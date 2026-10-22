import type { JSX } from 'react';

import { PageHeader } from '@src/shared/components/PageHeader';
import { Input } from '@src/shared/components/ui/Input';
import { Label } from '@src/shared/components/ui/label';

import { AccountsTable } from './components/AccountsTable';
import { NewAccountModal } from './components/NewAccountModal';
import {
  maximumAccountSearchLength,
  useAccountsSearchForm,
} from './hooks/useAccountsSearchForm';

export function Accounts(): JSX.Element {
  const { register, control, handleSubmit } = useAccountsSearchForm();

  return (
    <div className='flex flex-col gap-6'>
      <PageHeader
        title='Contas'
        description='Pessoas, saldos e limites de cheque especial.'
        actions={<NewAccountModal />}
      />

      <form
        role='search'
        onSubmit={handleSubmit}
        className='grid max-w-2xl gap-2'
        noValidate
      >
        <Label htmlFor='accounts-search'>Buscar por titular ou CPF</Label>
        <Input
          id='accounts-search'
          type='search'
          autoComplete='off'
          placeholder='Nome do titular ou CPF'
          maxLength={maximumAccountSearchLength}
          {...register('search')}
        />
      </form>

      <AccountsTable control={control} />
    </div>
  );
}
