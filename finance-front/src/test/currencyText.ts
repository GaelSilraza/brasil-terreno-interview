import { formatCurrency } from '@src/shared/utils';

export function currencyText(value: number): string {
  return formatCurrency(value).replace(/\u00a0/g, ' ');
}
