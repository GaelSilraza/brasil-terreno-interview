import { describe, expect, it } from 'vitest';

import { maskMoneyInput, parseMoneyInput } from './money';

describe('money input', () => {
  it('masks typed digits as cents with two decimals', () => {
    expect(maskMoneyInput('25050')).toBe('250,50');
    expect(maskMoneyInput('')).toBe('0,00');
    expect(maskMoneyInput('0001')).toBe('0,01');
    expect(maskMoneyInput('1234567')).toBe('12.345,67');
    expect(maskMoneyInput('9'.repeat(20))).toBe('9.999.999.999.999,99');
  });

  it('parses the masked value back to a number', () => {
    expect(parseMoneyInput('12.345,67')).toBe(12345.67);
    expect(parseMoneyInput('')).toBeNaN();
  });
});
