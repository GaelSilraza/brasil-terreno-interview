import { useCallback, type FormEvent } from 'react';
import { useForm, type Control, type UseFormRegister } from 'react-hook-form';

export const maximumAccountSearchLength = 100;

export interface AccountsSearchFormValues {
  search: string;
}

const defaultValues: AccountsSearchFormValues = { search: '' };

export interface IUseAccountsSearchFormResult {
  register: UseFormRegister<AccountsSearchFormValues>;
  control: Control<AccountsSearchFormValues>;
  handleSubmit: (event: FormEvent<HTMLFormElement>) => void;
}

export function useAccountsSearchForm(): IUseAccountsSearchFormResult {
  const { register, control } = useForm<AccountsSearchFormValues>({
    defaultValues,
  });

  const handleSubmit = useCallback(
    (event: FormEvent<HTMLFormElement>): void => {
      event.preventDefault();
    },
    []
  );

  return { register, control, handleSubmit };
}
