import { forwardRef, type ChangeEvent, type ComponentProps } from 'react';

import { Input } from '@src/shared/components/ui/Input';
import { maskMoneyInput } from '@src/shared/utils/money';

interface IMoneyInputProps extends Omit<
  ComponentProps<'input'>,
  'type' | 'inputMode'
> {
  error?: string;
}

export const MoneyInput = forwardRef<HTMLInputElement, IMoneyInputProps>(
  function MoneyInput({ onChange, ...props }, ref) {
    function handleChange(event: ChangeEvent<HTMLInputElement>): void {
      event.target.value = maskMoneyInput(event.target.value);
      onChange?.(event);
    }

    return (
      <Input
        {...props}
        ref={ref}
        type='text'
        inputMode='numeric'
        autoComplete='off'
        onChange={handleChange}
      />
    );
  }
);
