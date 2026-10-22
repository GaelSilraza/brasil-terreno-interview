import type { JSX } from 'react';

import { Button } from '@src/shared/components/ui/Button';
import { Input } from '@src/shared/components/ui/Input';
import { Label } from '@src/shared/components/ui/label';

import { useTransferLookupController } from '../hooks/useTransferLookupController';

export function TransferLookupForm(): JSX.Element {
  const { register, errors, handleSubmit } = useTransferLookupController();

  return (
    <form onSubmit={handleSubmit} className='grid gap-4' noValidate>
      <div className='grid gap-2'>
        <Label htmlFor='transfer-lookup-code'>Código da transferência</Label>
        <Input
          id='transfer-lookup-code'
          autoComplete='off'
          error={errors.transferId?.message}
          {...register('transferId')}
        />
      </div>
      <Button type='submit' variant='outline'>
        Consultar
      </Button>
    </form>
  );
}
