import { useCallback, useState, type BaseSyntheticEvent } from 'react';
import {
  useForm,
  type FieldErrors,
  type UseFormRegister,
} from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { z } from 'zod';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { UpdateOverdraftLimitDTO } from '@src/shared/dto';
import type { Account } from '@src/shared/entities';
import {
  applyApiErrorToForm,
  formatMoneyInput,
  getApiErrorMessage,
  moneySchema,
  toResultAsync,
} from '@src/shared/utils';

import { accountsService } from '../services';

const schema = z.object({
  overdraftLimit: moneySchema({
    allowZero: true,
    requiredMessage: 'Informe o limite de cheque especial.',
  }),
});

type FormInput = z.input<typeof schema>;
type FormOutput = z.output<typeof schema>;

const fieldNames = ['overdraftLimit'] as const;

export interface IUseEditOverdraftLimitControllerResult {
  register: UseFormRegister<FormInput>;
  errors: FieldErrors<FormInput>;
  handleSubmit: (event?: BaseSyntheticEvent) => Promise<void>;
  isPending: boolean;
  isModalOpen: boolean;
  handleChangeModal: (isOpen: boolean) => void;
}

export function useEditOverdraftLimitController(
  account: Account
): IUseEditOverdraftLimitControllerResult {
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit: hookFormSubmit,
    reset,
    setError,
    clearErrors,
    formState: { errors },
  } = useForm<FormInput, unknown, FormOutput>({
    resolver: zodResolver(schema),
    defaultValues: { overdraftLimit: formatMoneyInput(account.overdraftLimit) },
    mode: 'onSubmit',
    reValidateMode: 'onBlur',
  });

  const { isPending, mutateAsync } = useMutation({
    mutationFn: (overdraftLimitUpdate: UpdateOverdraftLimitDTO) =>
      accountsService.updateOverdraftLimit(overdraftLimitUpdate),
  });

  const accountId = account.id;
  const currentOverdraftLimit = account.overdraftLimit;

  const handleChangeModal = useCallback(
    (isOpen: boolean): void => {
      setIsModalOpen(isOpen);

      if (isOpen) {
        reset({ overdraftLimit: formatMoneyInput(currentOverdraftLimit) });
      }
    },
    [reset, currentOverdraftLimit]
  );

  const submitOverdraftLimit = useCallback(
    async ({ overdraftLimit }: FormOutput): Promise<void> => {
      clearErrors();
      const [error] = await toResultAsync(
        mutateAsync({ accountId, overdraftLimit })
      );

      if (error) {
        applyApiErrorToForm(error, setError, fieldNames);
        toast.error(getApiErrorMessage(error));
        return;
      }

      await queryClient.invalidateQueries({ queryKey: queryKeys.accounts.all });
      toast.success('Limite de cheque especial atualizado.');
      handleChangeModal(false);
    },
    [
      accountId,
      clearErrors,
      handleChangeModal,
      mutateAsync,
      queryClient,
      setError,
    ]
  );

  const handleSubmit = useCallback(
    (event?: BaseSyntheticEvent): Promise<void> =>
      hookFormSubmit(submitOverdraftLimit)(event),
    [hookFormSubmit, submitOverdraftLimit]
  );

  return {
    register,
    errors,
    handleSubmit,
    isPending,
    isModalOpen,
    handleChangeModal,
  };
}
