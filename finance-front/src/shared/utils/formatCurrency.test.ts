import { describe, expect, it } from 'vitest';

import { formatCurrency } from './formatCurrency';

describe('formatCurrency', () => {
  it('formats positive values as Brazilian reais', () => {
    expect(formatCurrency(1234.56)).toBe('R$ 1.234,56');
  });

  it('formats negative balances with the minus sign', () => {
    expect(formatCurrency(-800)).toBe('-R$ 800,00');
  });
});
