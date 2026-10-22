const moneyInputFormatter = new Intl.NumberFormat('pt-BR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export const maximumMoneyIntegerDigits = 13;

export function hasAtMostTwoDecimals(value: number): boolean {
  const valueInCents = value * 100;
  return Math.abs(valueInCents - Math.round(valueInCents)) < 1e-6;
}

export function formatMoneyInput(value: number): string {
  return moneyInputFormatter.format(value);
}

export function maskMoneyInput(rawValue: string): string {
  const digits = rawValue
    .replace(/\D/g, '')
    .replace(/^0+(?=\d)/, '')
    .slice(0, maximumMoneyIntegerDigits + 2);
  const cents = Number(digits === '' ? '0' : digits);
  return formatMoneyInput(cents / 100);
}

export function parseMoneyInput(maskedValue: string): number {
  const digits = maskedValue.replace(/\D/g, '');
  return digits === '' ? Number.NaN : Number(digits) / 100;
}
