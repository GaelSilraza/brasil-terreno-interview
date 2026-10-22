import { useId, type JSX } from 'react';
import {
  Controller,
  type Control,
  type FieldValues,
  type Path,
} from 'react-hook-form';

import { Label } from '@src/shared/components/ui/label';

import { AccountCombobox } from './AccountCombobox';

export interface IAccountSelectFieldProps<
  TFieldValues extends FieldValues,
  TTransformedValues extends FieldValues = TFieldValues,
> {
  control: Control<TFieldValues, unknown, TTransformedValues>;
  name: Path<TFieldValues>;
  label: string;
  placeholder: string;
  excludedAccountId?: string;
  error?: string;
}

export function AccountSelectField<
  TFieldValues extends FieldValues,
  TTransformedValues extends FieldValues = TFieldValues,
>({
  control,
  name,
  label,
  placeholder,
  excludedAccountId,
  error,
}: IAccountSelectFieldProps<TFieldValues, TTransformedValues>): JSX.Element {
  const triggerId = useId();
  const errorId = useId();

  return (
    <div className='grid gap-2'>
      <Label htmlFor={triggerId}>{label}</Label>
      <Controller
        control={control}
        name={name}
        render={({ field }) => (
          <AccountCombobox
            triggerId={triggerId}
            value={typeof field.value === 'string' ? field.value : ''}
            onChange={field.onChange}
            onBlur={field.onBlur}
            placeholder={placeholder}
            excludedAccountId={excludedAccountId}
            errorId={error ? errorId : undefined}
          />
        )}
      />
      {error && (
        <span id={errorId} className='text-destructive text-sm'>
          {error}
        </span>
      )}
    </div>
  );
}
