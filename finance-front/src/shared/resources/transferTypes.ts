export const transferTypes = ['Immediate', 'Scheduled'] as const;

export const transferTypeLabels: Record<
  (typeof transferTypes)[number],
  string
> = {
  Immediate: 'Imediata',
  Scheduled: 'Agendada',
};
