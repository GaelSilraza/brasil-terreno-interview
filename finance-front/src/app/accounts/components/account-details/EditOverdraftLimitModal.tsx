import type { JSX } from 'react';
import { Pencil } from 'lucide-react';

import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { MoneyInput } from '@src/shared/components/MoneyInput';
import { Button } from '@src/shared/components/ui/Button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@src/shared/components/ui/Dialog';
import { Label } from '@src/shared/components/ui/label';
import type { Account } from '@src/shared/entities';

import { useEditOverdraftLimitController } from '../../hooks/useEditOverdraftLimitController';

export interface IEditOverdraftLimitModalProps {
  account: Account;
}

export function EditOverdraftLimitModal({
  account,
}: IEditOverdraftLimitModalProps): JSX.Element {
  const {
    register,
    errors,
    handleSubmit,
    isPending,
    isModalOpen,
    handleChangeModal,
  } = useEditOverdraftLimitController(account);

  return (
    <Dialog open={isModalOpen} onOpenChange={handleChangeModal}>
      <DialogTrigger asChild>
        <Button variant='outline' size='sm' className='cursor-pointer'>
          <Pencil />
          Alterar cheque especial
        </Button>
      </DialogTrigger>
      <DialogContent className='sm:max-w-[425px]'>
        <DialogHeader className='mb-5'>
          <DialogTitle>Alterar cheque especial</DialogTitle>
          <DialogDescription>
            Informe o novo limite de cheque especial de {account.holderName}.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} noValidate>
          <div className='grid gap-4'>
            <div className='grid gap-2'>
              <Label htmlFor='edit-overdraft-limit'>
                Novo limite de cheque especial (R$)
              </Label>
              <MoneyInput
                id='edit-overdraft-limit'
                {...register('overdraftLimit')}
                error={errors.overdraftLimit?.message}
              />
            </div>

            {errors.root?.message && (
              <ApiErrorAlert
                title='Não foi possível alterar o limite'
                message={errors.root.message}
              />
            )}

            <Button
              type='submit'
              className='mt-4 w-full cursor-pointer'
              loading={{ isLoading: isPending, message: 'Salvando...' }}
            >
              Salvar limite
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
