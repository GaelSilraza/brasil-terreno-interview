import type { JSX } from 'react';

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
import type { Transfer } from '@src/shared/entities';

import { useCancelTransferController } from '../../hooks/useCancelTransferController';

export interface ICancelTransferButtonProps {
  transfer: Transfer;
}

export function CancelTransferButton({
  transfer,
}: ICancelTransferButtonProps): JSX.Element {
  const { isDialogOpen, handleChangeDialog, handleConfirm, isPending } =
    useCancelTransferController(transfer);

  return (
    <Dialog open={isDialogOpen} onOpenChange={handleChangeDialog}>
      <DialogTrigger asChild>
        <Button variant='destructive'>Cancelar agendamento</Button>
      </DialogTrigger>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Cancelar transferência agendada?</DialogTitle>
          <DialogDescription>
            A transferência não será executada.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button
            type='button'
            variant='outline'
            disabled={isPending}
            onClick={() => handleChangeDialog(false)}
          >
            Voltar
          </Button>
          <Button
            type='button'
            variant='destructive'
            loading={{ isLoading: isPending, message: 'Cancelando...' }}
            onClick={handleConfirm}
          >
            Confirmar cancelamento
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
