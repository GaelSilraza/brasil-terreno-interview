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
import type { CreateAccountDTO } from '@src/shared/dto';
import {
  applyApiErrorToForm,
  documentNumberSchema,
  getApiErrorMessage,
  moneySchema,
  toResultAsync,
} from '@src/shared/utils';

import { accountsService } from '../services';

const schema = z.object({
  fullName: z
    .string()
    .trim()
    .min(3, 'Informe o nome completo.')
    .max(150, 'Use no máximo 150 caracteres.'),
  documentNumber: documentNumberSchema,
  initialBalance: moneySchema({
    allowZero: true,
    requiredMessage: 'Informe o saldo inicial.',
  }),
  overdraftLimit: moneySchema({
    allowZero: true,
    requiredMessage: 'Informe o limite de cheque especial.',
  }),
});

type FormInput = z.input<typeof schema>;
type FormOutput = z.output<typeof schema>;

const defaultValues: FormInput = {
  fullName: '',
  documentNumber: '',
  initialBalance: '0,00',
  overdraftLimit: '0,00',
};

const fieldNames = [
  'fullName',
  'documentNumber',
  'initialBalance',
  'overdraftLimit',
] as const;

export interface IUseNewAccountControllerResult {
  register: UseFormRegister<FormInput>;
  errors: FieldErrors<FormInput>;
  handleSubmit: (event?: BaseSyntheticEvent) => Promise<void>;
  isPending: boolean;
  isModalOpen: boolean;
  handleChangeModal: (isOpen: boolean) => void;
}

export function useNewAccountController(): IUseNewAccountControllerResult {
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
    defaultValues,
    mode: 'onSubmit',
    reValidateMode: 'onBlur',
  });

  const { isPending, mutateAsync } = useMutation({
    mutationFn: (account: CreateAccountDTO) => accountsService.create(account),
  });

  const handleChangeModal = useCallback(
    (isOpen: boolean): void => {
      setIsModalOpen(isOpen);

      if (!isOpen) {
        reset(defaultValues);
      }
    },
    [reset]
  );

  const submitAccount = useCallback(
    async (account: FormOutput): Promise<void> => {
      clearErrors();
      const [error] = await toResultAsync(mutateAsync(account));

      if (error) {
        applyApiErrorToForm(error, setError, fieldNames);
        toast.error(getApiErrorMessage(error));
        return;
      }

      await queryClient.invalidateQueries({ queryKey: queryKeys.accounts.all });
      toast.success('Conta criada com sucesso.');
      handleChangeModal(false);
    },
    [clearErrors, handleChangeModal, mutateAsync, queryClient, setError]
  );

  const handleSubmit = useCallback(
    (event?: BaseSyntheticEvent): Promise<void> =>
      hookFormSubmit(submitAccount)(event),
    [hookFormSubmit, submitAccount]
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
