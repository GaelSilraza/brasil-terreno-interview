import { useId, type JSX } from 'react';
import { Controller } from 'react-hook-form';

import { Button } from '@src/shared/components/ui/Button';
import { Label } from '@src/shared/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@src/shared/components/ui/Select';
import type { Account } from '@src/shared/entities';
import { accountStatuses, accountStatusLabels } from '@src/shared/resources';

import { useChangeAccountStatusController } from '../../hooks/useChangeAccountStatusController';

export interface IAccountStatusFormProps {
  account: Account;
}

export function AccountStatusForm({
  account,
}: IAccountStatusFormProps): JSX.Element {
  const triggerId = useId();
  const { control, handleSubmit, isPending } =
    useChangeAccountStatusController(account);

  return (
    <form onSubmit={handleSubmit} className='grid gap-2'>
      <Label htmlFor={triggerId}>Alterar status</Label>
      <Controller
        control={control}
        name='status'
        render={({ field }) => (
          <Select value={field.value} onValueChange={field.onChange}>
            <SelectTrigger id={triggerId} className='w-full'>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {accountStatuses.map((accountStatus) => (
                <SelectItem key={accountStatus} value={accountStatus}>
                  {accountStatusLabels[accountStatus]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      />
      <Button
        type='submit'
        size='sm'
        className='cursor-pointer'
        loading={{ isLoading: isPending, message: 'Atualizando...' }}
      >
        Atualizar status
      </Button>
    </form>
  );
}
