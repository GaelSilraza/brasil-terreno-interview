import { useCallback, type BaseSyntheticEvent } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate } from '@tanstack/react-router';
import {
  useForm,
  type FieldErrors,
  type UseFormRegister,
} from 'react-hook-form';
import { z } from 'zod';

const schema = z.object({
  transferId: z
    .string()
    .trim()
    .pipe(z.uuid('Informe um código de transferência válido.')),
});

type FormValues = z.infer<typeof schema>;

export interface IUseTransferLookupControllerResult {
  register: UseFormRegister<FormValues>;
  errors: FieldErrors<FormValues>;
  handleSubmit: (event?: BaseSyntheticEvent) => Promise<void>;
}

export function useTransferLookupController(): IUseTransferLookupControllerResult {
  const navigate = useNavigate();

  const {
    register,
    formState,
    handleSubmit: hookFormSubmit,
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { transferId: '' },
    mode: 'onSubmit',
    reValidateMode: 'onBlur',
  });
  const { errors } = formState;

  const submitLookup = useCallback(
    async (formValues: FormValues): Promise<void> => {
      await navigate({
        to: '/transfers/$transferId',
        params: { transferId: formValues.transferId },
      });
    },
    [navigate]
  );

  const handleSubmit = useCallback(
    (event?: BaseSyntheticEvent): Promise<void> =>
      hookFormSubmit(submitLookup)(event),
    [hookFormSubmit, submitLookup]
  );

  return { register, errors, handleSubmit };
}
