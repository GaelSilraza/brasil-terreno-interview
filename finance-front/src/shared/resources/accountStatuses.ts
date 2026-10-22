export const accountStatuses = ['Active', 'Blocked', 'Inactive'] as const;

export const accountStatusLabels: Record<
  (typeof accountStatuses)[number],
  string
> = {
  Active: 'Ativa',
  Blocked: 'Bloqueada',
  Inactive: 'Inativa',
};
