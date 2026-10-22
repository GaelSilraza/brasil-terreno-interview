import type { JSX } from 'react';

import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { MoneyInput } from '@src/shared/components/MoneyInput';
import { Button } from '@src/shared/components/ui/Button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@src/shared/components/ui/Dialog';
import { Input } from '@src/shared/components/ui/Input';
import { Label } from '@src/shared/components/ui/label';
import type { TransferLimits } from '@src/shared/entities';

import { useEditTransferLimitsController } from '../hooks/useEditTransferLimitsController';

export interface IEditTransferLimitsModalProps {
  limits: TransferLimits;
}

export function EditTransferLimitsModal({
  limits,
}: IEditTransferLimitsModalProps): JSX.Element {
  const {
    register,
    errors,
    handleSubmit,
    isPending,
    isModalOpen,
    handleChangeModal,
  } = useEditTransferLimitsController(limits);

  return (
    <Dialog open={isModalOpen} onOpenChange={handleChangeModal}>
      <DialogTrigger asChild>
        <Button variant='outline' className='w-fit'>
          Alterar limites
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Alterar limites por hora</DialogTitle>
          <DialogDescription>
            Defina o valor máximo e o número de tentativas por hora em cada
            período.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className='grid gap-4' noValidate>
          <div className='grid gap-2'>
            <Label htmlFor='daytime-max-amount-per-hour'>
              Valor máximo diurno (R$)
            </Label>
            <MoneyInput
              id='daytime-max-amount-per-hour'
              error={errors.daytimeMaxAmountPerHour?.message}
              {...register('daytimeMaxAmountPerHour')}
            />
          </div>
          <div className='grid gap-2'>
            <Label htmlFor='daytime-max-attempts-per-hour'>
              Tentativas diurnas por hora
            </Label>
            <Input
              id='daytime-max-attempts-per-hour'
              type='number'
              step='1'
              inputMode='numeric'
              error={errors.daytimeMaxAttemptsPerHour?.message}
              {...register('daytimeMaxAttemptsPerHour', {
                valueAsNumber: true,
              })}
            />
          </div>
          <div className='grid gap-2'>
            <Label htmlFor='nighttime-max-amount-per-hour'>
              Valor máximo noturno (R$)
            </Label>
            <MoneyInput
              id='nighttime-max-amount-per-hour'
              error={errors.nighttimeMaxAmountPerHour?.message}
              {...register('nighttimeMaxAmountPerHour')}
            />
          </div>
          <div className='grid gap-2'>
            <Label htmlFor='nighttime-max-attempts-per-hour'>
              Tentativas noturnas por hora
            </Label>
            <Input
              id='nighttime-max-attempts-per-hour'
              type='number'
              step='1'
              inputMode='numeric'
              error={errors.nighttimeMaxAttemptsPerHour?.message}
              {...register('nighttimeMaxAttemptsPerHour', {
                valueAsNumber: true,
              })}
            />
          </div>
          {errors.root?.message && (
            <ApiErrorAlert
              title='Não foi possível salvar os limites'
              message={errors.root.message}
            />
          )}
          <DialogFooter>
            <Button
              type='submit'
              loading={{ isLoading: isPending, message: 'Salvando...' }}
            >
              Salvar limites
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
