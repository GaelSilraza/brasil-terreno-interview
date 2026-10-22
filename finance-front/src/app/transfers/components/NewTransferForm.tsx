import type { JSX } from 'react';

import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { MoneyInput } from '@src/shared/components/MoneyInput';
import { Button } from '@src/shared/components/ui/Button';
import { Label } from '@src/shared/components/ui/label';

import { useNewTransferController } from '../hooks/useNewTransferController';
import { DestinationAccountField } from './DestinationAccountField';
import { SourceAccountField } from './SourceAccountField';

export interface INewTransferFormProps {
  initialSourceAccountId: string | undefined;
}

export function NewTransferForm({
  initialSourceAccountId,
}: INewTransferFormProps): JSX.Element {
  const { register, control, errors, handleSubmit, isPending } =
    useNewTransferController(initialSourceAccountId);

  return (
    <form onSubmit={handleSubmit} className='grid gap-4' noValidate>
      <SourceAccountField
        control={control}
        name='sourceAccountId'
        error={errors.sourceAccountId?.message}
      />
      <DestinationAccountField
        control={control}
        name='destinationAccountId'
        sourceAccountFieldName='sourceAccountId'
        error={errors.destinationAccountId?.message}
      />
      <div className='grid gap-2'>
        <Label htmlFor='immediate-transfer-amount'>Valor (R$)</Label>
        <MoneyInput
          id='immediate-transfer-amount'
          error={errors.amount?.message}
          {...register('amount')}
        />
      </div>
      {errors.root?.message && (
        <ApiErrorAlert
          title='Não foi possível transferir'
          message={errors.root.message}
        />
      )}
      <Button
        type='submit'
        loading={{ isLoading: isPending, message: 'Transferindo...' }}
      >
        Transferir
      </Button>
    </form>
  );
}
