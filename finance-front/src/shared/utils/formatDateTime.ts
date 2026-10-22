const dateTimeFormatter = new Intl.DateTimeFormat('pt-BR', {
  dateStyle: 'short',
  timeStyle: 'short',
});

export function formatDateTime(isoDate: string): string {
  return dateTimeFormatter.format(new Date(isoDate));
}

export function toIsoFromDateTimeLocal(dateTimeLocalValue: string): string {
  return new Date(dateTimeLocalValue).toISOString();
}

export function toDateTimeLocalValue(date: Date): string {
  const offsetInMilliseconds = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetInMilliseconds)
    .toISOString()
    .slice(0, 16);
}
