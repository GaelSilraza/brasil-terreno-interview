import { useCallback, useRef, type BaseSyntheticEvent } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import {
  useForm,
  type Control,
  type FieldErrors,
  type UseFormRegister,
} from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { queryKeys } from '@src/shared/configs/queryKeys';
import {
  applyApiErrorToForm,
  createIdempotencyKey,
  getApiErrorMessage,
  getApiProblem,
  moneySchema,
  toDateTimeLocalValue,
  toIsoFromDateTimeLocal,
  toResultAsync,
} from '@src/shared/utils';

import { transfersService } from '../services';
import { isRetryableFailure } from './idempotency';

const schema = z
  .object({
    sourceAccountId: z.string().min(1, 'Selecione a conta de origem.'),
    destinationAccountId: z.string().min(1, 'Selecione a conta de destino.'),
    amount: moneySchema({
      allowZero: false,
      requiredMessage: 'Informe o valor.',
    }),
    scheduledFor: z
      .string()
      .min(1, 'Informe a data e a hora.')
      .refine(
        (dateTimeLocalValue) => new Date(dateTimeLocalValue) > new Date(),
        'A data deve ser futura.'
      ),
  })
  .refine(
    (formValues) =>
      formValues.sourceAccountId !== formValues.destinationAccountId,
    {
      message: 'A conta de origem e a de destino devem ser diferentes.',
      path: ['destinationAccountId'],
    }
  );

type FormInput = z.input<typeof schema>;
type FormValues = z.output<typeof schema>;

export interface IUseScheduleTransferControllerResult {
  register: UseFormRegister<FormInput>;
  control: Control<FormInput, unknown, FormValues>;
  errors: FieldErrors<FormInput>;
  handleSubmit: (event?: BaseSyntheticEvent) => Promise<void>;
  isPending: boolean;
}

const oneHourInMilliseconds = 60 * 60 * 1000;

function createDefaultScheduledFor(): string {
  return toDateTimeLocalValue(new Date(Date.now() + oneHourInMilliseconds));
}

export function useScheduleTransferController(
  initialSourceAccountId: string | undefined
): IUseScheduleTransferControllerResult {
  const idempotencyKeyRef = useRef<string>(createIdempotencyKey());
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const {
    register,
    control,
    formState,
    handleSubmit: hookFormSubmit,
    clearErrors,
    setError,
    reset,
  } = useForm<FormInput, unknown, FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      sourceAccountId: initialSourceAccountId ?? '',
      destinationAccountId: '',
      amount: '0,00',
      scheduledFor: createDefaultScheduledFor(),
    },
    mode: 'onSubmit',
    reValidateMode: 'onBlur',
  });
  const { errors } = formState;

  const { mutateAsync, isPending } = useMutation({
    mutationFn: transfersService.schedule,
  });

  const submitTransfer = useCallback(
    async (formValues: FormValues): Promise<void> => {
      clearErrors();

      const [error, transfer] = await toResultAsync(
        mutateAsync({
          ...formValues,
          scheduledFor: toIsoFromDateTimeLocal(formValues.scheduledFor),
          idempotencyKey: idempotencyKeyRef.current,
        })
      );

      if (error) {
        if (!isRetryableFailure(error)) {
          idempotencyKeyRef.current = createIdempotencyKey();
        }

        const problem = getApiProblem(error);

        if (problem?.transferId) {
          toast.error(getApiErrorMessage(error));
          void queryClient.invalidateQueries({
            queryKey: queryKeys.accounts.all,
          });
          void queryClient.invalidateQueries({
            queryKey: queryKeys.transfers.all,
          });
          await navigate({
            to: '/transfers/$transferId',
            params: { transferId: problem.transferId },
          });
          return;
        }

        applyApiErrorToForm(error, setError, [
          'sourceAccountId',
          'destinationAccountId',
          'amount',
          'scheduledFor',
        ]);
        toast.error(getApiErrorMessage(error));
        return;
      }

      if (!transfer) {
        return;
      }

      idempotencyKeyRef.current = createIdempotencyKey();
      void queryClient.invalidateQueries({ queryKey: queryKeys.accounts.all });
      void queryClient.invalidateQueries({ queryKey: queryKeys.transfers.all });
      toast.success('Transferência agendada.');
      reset({
        sourceAccountId: formValues.sourceAccountId,
        destinationAccountId: '',
        amount: '0,00',
        scheduledFor: createDefaultScheduledFor(),
      });
      await navigate({
        to: '/transfers/$transferId',
        params: { transferId: transfer.id },
      });
    },
    [clearErrors, mutateAsync, navigate, queryClient, reset, setError]
  );

  const handleSubmit = useCallback(
    (event?: BaseSyntheticEvent): Promise<void> =>
      hookFormSubmit(submitTransfer)(event),
    [hookFormSubmit, submitTransfer]
  );

  return { register, control, errors, handleSubmit, isPending };
}
