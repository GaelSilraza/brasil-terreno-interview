import { useCallback, useState, type BaseSyntheticEvent } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  useForm,
  type FieldErrors,
  type UseFormRegister,
} from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { TransferLimits } from '@src/shared/entities';
import {
  applyApiErrorToForm,
  formatMoneyInput,
  getApiErrorMessage,
  moneySchema,
  toResultAsync,
} from '@src/shared/utils';

import { transfersService } from '../services';

const maxAmountPerHourSchema = moneySchema({
  allowZero: false,
  requiredMessage: 'Informe o valor.',
});

const maxAttemptsPerHourSchema = z
  .number({ error: 'Informe o número de tentativas.' })
  .int('Use um número inteiro.')
  .min(1, 'Informe no mínimo 1 tentativa.')
  .max(1000, 'Informe no máximo 1000 tentativas.');

const schema = z.object({
  daytimeMaxAmountPerHour: maxAmountPerHourSchema,
  daytimeMaxAttemptsPerHour: maxAttemptsPerHourSchema,
  nighttimeMaxAmountPerHour: maxAmountPerHourSchema,
  nighttimeMaxAttemptsPerHour: maxAttemptsPerHourSchema,
});

type FormInput = z.input<typeof schema>;
type FormValues = z.output<typeof schema>;

export interface IUseEditTransferLimitsControllerResult {
  register: UseFormRegister<FormInput>;
  errors: FieldErrors<FormInput>;
  handleSubmit: (event?: BaseSyntheticEvent) => Promise<void>;
  isPending: boolean;
  isModalOpen: boolean;
  handleChangeModal: (isOpen: boolean) => void;
}

function toFormInput(limits: TransferLimits): FormInput {
  return {
    daytimeMaxAmountPerHour: formatMoneyInput(limits.daytime.maxAmountPerHour),
    daytimeMaxAttemptsPerHour: limits.daytime.maxAttemptsPerHour,
    nighttimeMaxAmountPerHour: formatMoneyInput(
      limits.nighttime.maxAmountPerHour
    ),
    nighttimeMaxAttemptsPerHour: limits.nighttime.maxAttemptsPerHour,
  };
}

export function useEditTransferLimitsController(
  limits: TransferLimits
): IUseEditTransferLimitsControllerResult {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const queryClient = useQueryClient();

  const {
    register,
    formState,
    handleSubmit: hookFormSubmit,
    clearErrors,
    setError,
    reset,
  } = useForm<FormInput, unknown, FormValues>({
    resolver: zodResolver(schema),
    defaultValues: toFormInput(limits),
    mode: 'onSubmit',
    reValidateMode: 'onBlur',
  });
  const { errors } = formState;

  const { mutateAsync, isPending } = useMutation({
    mutationFn: transfersService.updateLimits,
  });

  const handleChangeModal = useCallback(
    (isOpen: boolean): void => {
      if (isOpen) {
        reset(toFormInput(limits));
      }

      setIsModalOpen(isOpen);
    },
    [limits, reset]
  );

  const submitLimits = useCallback(
    async (formValues: FormValues): Promise<void> => {
      clearErrors();

      const [error] = await toResultAsync(
        mutateAsync({ accountId: limits.accountId, ...formValues })
      );

      if (error) {
        applyApiErrorToForm(error, setError, [
          'daytimeMaxAmountPerHour',
          'daytimeMaxAttemptsPerHour',
          'nighttimeMaxAmountPerHour',
          'nighttimeMaxAttemptsPerHour',
        ]);
        toast.error(getApiErrorMessage(error));
        return;
      }

      void queryClient.invalidateQueries({
        queryKey: queryKeys.transfers.limits(limits.accountId),
      });
      toast.success('Limites atualizados.');
      setIsModalOpen(false);
    },
    [clearErrors, limits.accountId, mutateAsync, queryClient, setError]
  );

  const handleSubmit = useCallback(
    (event?: BaseSyntheticEvent): Promise<void> =>
      hookFormSubmit(submitLimits)(event),
    [hookFormSubmit, submitLimits]
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
