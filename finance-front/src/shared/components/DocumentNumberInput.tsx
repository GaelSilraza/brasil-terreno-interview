import { forwardRef, type ChangeEvent, type ComponentProps } from 'react';

import { Input } from '@src/shared/components/ui/Input';
import { maskDocumentNumber } from '@src/shared/utils/documentNumber';

interface IDocumentNumberInputProps extends Omit<
  ComponentProps<'input'>,
  'type' | 'inputMode' | 'maxLength'
> {
  error?: string;
}

export const DocumentNumberInput = forwardRef<
  HTMLInputElement,
  IDocumentNumberInputProps
>(function DocumentNumberInput({ onChange, ...props }, ref) {
  function handleChange(event: ChangeEvent<HTMLInputElement>): void {
    event.target.value = maskDocumentNumber(event.target.value);
    onChange?.(event);
  }

  return (
    <Input
      {...props}
      ref={ref}
      type='text'
      inputMode='numeric'
      maxLength={14}
      autoComplete='off'
      onChange={handleChange}
    />
  );
});
