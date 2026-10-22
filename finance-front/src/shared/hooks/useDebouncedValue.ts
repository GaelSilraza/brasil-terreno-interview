import { useEffect, useState } from 'react';

export const searchDebounceInMilliseconds = 300;

export function useDebouncedValue<TValue>(
  value: TValue,
  delayInMilliseconds: number = searchDebounceInMilliseconds
): TValue {
  const [debouncedValue, setDebouncedValue] = useState<TValue>(value);

  useEffect(() => {
    const timeoutId = window.setTimeout(
      () => setDebouncedValue(value),
      delayInMilliseconds
    );
    return () => window.clearTimeout(timeoutId);
  }, [value, delayInMilliseconds]);

  return debouncedValue;
}
