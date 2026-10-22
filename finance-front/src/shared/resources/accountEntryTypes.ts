export const accountEntryTypes = ['Debit', 'Credit'] as const;

export const accountEntryTypeLabels: Record<
  (typeof accountEntryTypes)[number],
  string
> = {
  Debit: 'Débito',
  Credit: 'Crédito',
};
