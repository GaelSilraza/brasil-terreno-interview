import type { JSX } from 'react';
import type { Control, FieldValues, Path } from 'react-hook-form';

import { AccountSelectField } from '@src/app/accounts/components/AccountSelectField';

interface ISourceAccountFieldProps<
  TFieldValues extends FieldValues,
  TTransformedValues extends FieldValues,
> {
  control: Control<TFieldValues, unknown, TTransformedValues>;
  name: Path<TFieldValues>;
  error?: string;
}

export function SourceAccountField<
  TFieldValues extends FieldValues,
  TTransformedValues extends FieldValues,
>({
  control,
  name,
  error,
}: ISourceAccountFieldProps<TFieldValues, TTransformedValues>): JSX.Element {
  return (
    <AccountSelectField
      control={control}
      name={name}
      label='Conta de origem'
      placeholder='Selecione a conta de origem'
      error={error}
    />
  );
}
