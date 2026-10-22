import type { JSX } from 'react';
import { Plus } from 'lucide-react';

import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { Button } from '@src/shared/components/ui/Button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@src/shared/components/ui/Dialog';
import { DocumentNumberInput } from '@src/shared/components/DocumentNumberInput';
import { MoneyInput } from '@src/shared/components/MoneyInput';
import { Input } from '@src/shared/components/ui/Input';
import { Label } from '@src/shared/components/ui/label';

import { useNewAccountController } from '../hooks/useNewAccountController';

export function NewAccountModal(): JSX.Element {
  const {
    register,
    errors,
    handleSubmit,
    isPending,
    isModalOpen,
    handleChangeModal,
  } = useNewAccountController();

  return (
    <Dialog open={isModalOpen} onOpenChange={handleChangeModal}>
      <DialogTrigger asChild>
        <Button className='w-auto cursor-pointer p-4' size='sm'>
          <Plus />
          Nova conta
        </Button>
      </DialogTrigger>
      <DialogContent className='sm:max-w-[425px]'>
        <DialogHeader className='mb-5'>
          <DialogTitle>Nova conta</DialogTitle>
          <DialogDescription>
            Preencha os dados abaixo para abrir uma nova conta.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} noValidate>
          <div className='grid gap-4'>
            <div className='grid gap-2'>
              <Label htmlFor='new-account-full-name'>Nome completo</Label>
              <Input
                id='new-account-full-name'
                autoComplete='off'
                {...register('fullName')}
                error={errors.fullName?.message}
              />
            </div>

            <div className='grid gap-2'>
              <Label htmlFor='new-account-document-number'>CPF</Label>
              <DocumentNumberInput
                id='new-account-document-number'
                placeholder='000.000.000-00'
                {...register('documentNumber')}
                error={errors.documentNumber?.message}
              />
            </div>

            <div className='grid gap-2'>
              <Label htmlFor='new-account-initial-balance'>
                Saldo inicial (R$)
              </Label>
              <MoneyInput
                id='new-account-initial-balance'
                {...register('initialBalance')}
                error={errors.initialBalance?.message}
              />
            </div>

            <div className='grid gap-2'>
              <Label htmlFor='new-account-overdraft-limit'>
                Limite de cheque especial (R$)
              </Label>
              <MoneyInput
                id='new-account-overdraft-limit'
                {...register('overdraftLimit')}
                error={errors.overdraftLimit?.message}
              />
            </div>

            {errors.root?.message && (
              <ApiErrorAlert
                title='Não foi possível criar a conta'
                message={errors.root.message}
              />
            )}

            <Button
              type='submit'
              className='mt-4 w-full cursor-pointer'
              loading={{ isLoading: isPending, message: 'Criando...' }}
            >
              Criar conta
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
