import { useCallback, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { Transfer } from '@src/shared/entities';
import { getApiErrorMessage, toResultAsync } from '@src/shared/utils';

import { transfersService } from '../services';

export interface IUseCancelTransferControllerResult {
  isDialogOpen: boolean;
  handleChangeDialog: (isOpen: boolean) => void;
  handleConfirm: () => Promise<void>;
  isPending: boolean;
}

export function useCancelTransferController(
  transfer: Transfer
): IUseCancelTransferControllerResult {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const queryClient = useQueryClient();
  const { mutateAsync, isPending } = useMutation({
    mutationFn: transfersService.cancel,
  });

  const handleChangeDialog = useCallback((isOpen: boolean): void => {
    setIsDialogOpen(isOpen);
  }, []);

  const handleConfirm = useCallback(async (): Promise<void> => {
    const [error] = await toResultAsync(mutateAsync(transfer.id));

    void queryClient.invalidateQueries({ queryKey: queryKeys.accounts.all });
    void queryClient.invalidateQueries({ queryKey: queryKeys.transfers.all });
    setIsDialogOpen(false);

    if (error) {
      toast.error(getApiErrorMessage(error));
      return;
    }

    toast.success('Transferência cancelada.');
  }, [mutateAsync, queryClient, transfer.id]);

  return { isDialogOpen, handleChangeDialog, handleConfirm, isPending };
}
