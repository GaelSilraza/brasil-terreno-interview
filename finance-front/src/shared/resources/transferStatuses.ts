export const transferStatuses = [
  'Scheduled',
  'Processing',
  'Completed',
  'Failed',
  'Cancelled',
] as const;

export const transferStatusLabels: Record<
  (typeof transferStatuses)[number],
  string
> = {
  Scheduled: 'Agendada',
  Processing: 'Em processamento',
  Completed: 'Concluída',
  Failed: 'Falhou',
  Cancelled: 'Cancelada',
};
