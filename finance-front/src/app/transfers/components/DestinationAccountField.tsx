import type { JSX } from 'react';
import {
  useWatch,
  type Control,
  type FieldValues,
  type Path,
} from 'react-hook-form';

import { AccountSelectField } from '@src/app/accounts/components/AccountSelectField';

interface IDestinationAccountFieldProps<
  TFieldValues extends FieldValues,
  TTransformedValues extends FieldValues,
> {
  control: Control<TFieldValues, unknown, TTransformedValues>;
  name: Path<TFieldValues>;
  sourceAccountFieldName: Path<TFieldValues>;
  error?: string;
}

export function DestinationAccountField<
  TFieldValues extends FieldValues,
  TTransformedValues extends FieldValues,
>({
  control,
  name,
  sourceAccountFieldName,
  error,
}: IDestinationAccountFieldProps<
  TFieldValues,
  TTransformedValues
>): JSX.Element {
  const sourceAccountId: unknown = useWatch({
    control,
    name: sourceAccountFieldName,
  });

  return (
    <AccountSelectField
      control={control}
      name={name}
      label='Conta de destino'
      placeholder='Selecione a conta de destino'
      excludedAccountId={
        typeof sourceAccountId === 'string' ? sourceAccountId : undefined
      }
      error={error}
    />
  );
}
