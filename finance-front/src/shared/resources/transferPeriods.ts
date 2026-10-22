export const transferPeriods = ['Daytime', 'Nighttime'] as const;

export const transferPeriodLabels: Record<
  (typeof transferPeriods)[number],
  string
> = {
  Daytime: 'Diurno',
  Nighttime: 'Noturno',
};
