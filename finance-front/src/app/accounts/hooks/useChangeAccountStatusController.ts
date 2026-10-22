import { useCallback, type BaseSyntheticEvent } from 'react';
import { useForm, type Control } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { z } from 'zod';

import { queryKeys } from '@src/shared/configs/queryKeys';
import type { UpdateAccountStatusDTO } from '@src/shared/dto';
import type { Account } from '@src/shared/entities';
import { accountStatuses } from '@src/shared/resources';
import {
  applyApiErrorToForm,
  getApiErrorMessage,
  toResultAsync,
} from '@src/shared/utils';

import { accountsService } from '../services';

const schema = z.object({
  status: z.enum(accountStatuses),
});

type FormValues = z.infer<typeof schema>;

const fieldNames = ['status'] as const;

export interface IUseChangeAccountStatusControllerResult {
  control: Control<FormValues>;
  handleSubmit: (event?: BaseSyntheticEvent) => Promise<void>;
  isPending: boolean;
}

export function useChangeAccountStatusController(
  account: Account
): IUseChangeAccountStatusControllerResult {
  const queryClient = useQueryClient();
  const {
    control,
    handleSubmit: hookFormSubmit,
    setError,
    clearErrors,
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { status: account.status },
    mode: 'onSubmit',
    reValidateMode: 'onBlur',
  });

  const { isPending, mutateAsync } = useMutation({
    mutationFn: (statusUpdate: UpdateAccountStatusDTO) =>
      accountsService.updateStatus(statusUpdate),
  });

  const accountId = account.id;

  const submitStatus = useCallback(
    async ({ status }: FormValues): Promise<void> => {
      clearErrors();
      const [error] = await toResultAsync(mutateAsync({ accountId, status }));

      if (error) {
        applyApiErrorToForm(error, setError, fieldNames);
        toast.error(getApiErrorMessage(error));
        return;
      }

      await queryClient.invalidateQueries({ queryKey: queryKeys.accounts.all });
      toast.success('Status da conta atualizado.');
    },
    [accountId, clearErrors, mutateAsync, queryClient, setError]
  );

  const handleSubmit = useCallback(
    (event?: BaseSyntheticEvent): Promise<void> =>
      hookFormSubmit(submitStatus)(event),
    [hookFormSubmit, submitStatus]
  );

  return { control, handleSubmit, isPending };
}
