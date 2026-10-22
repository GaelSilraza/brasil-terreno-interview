import type { JSX } from 'react';

import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { MoneyInput } from '@src/shared/components/MoneyInput';
import { Button } from '@src/shared/components/ui/Button';
import { Input } from '@src/shared/components/ui/Input';
import { Label } from '@src/shared/components/ui/label';

import { useScheduleTransferController } from '../hooks/useScheduleTransferController';
import { DestinationAccountField } from './DestinationAccountField';
import { SourceAccountField } from './SourceAccountField';

export interface IScheduleTransferFormProps {
  initialSourceAccountId: string | undefined;
}

export function ScheduleTransferForm({
  initialSourceAccountId,
}: IScheduleTransferFormProps): JSX.Element {
  const { register, control, errors, handleSubmit, isPending } =
    useScheduleTransferController(initialSourceAccountId);

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
        <Label htmlFor='scheduled-transfer-amount'>Valor (R$)</Label>
        <MoneyInput
          id='scheduled-transfer-amount'
          error={errors.amount?.message}
          {...register('amount')}
        />
      </div>
      <div className='grid gap-2'>
        <Label htmlFor='scheduled-transfer-date'>Data e hora</Label>
        <Input
          id='scheduled-transfer-date'
          type='datetime-local'
          error={errors.scheduledFor?.message}
          {...register('scheduledFor')}
        />
      </div>
      {errors.root?.message && (
        <ApiErrorAlert
          title='Não foi possível agendar'
          message={errors.root.message}
        />
      )}
      <Button
        type='submit'
        loading={{ isLoading: isPending, message: 'Agendando...' }}
      >
        Agendar
      </Button>
    </form>
  );
}
