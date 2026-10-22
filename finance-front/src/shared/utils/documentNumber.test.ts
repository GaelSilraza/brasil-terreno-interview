import { describe, expect, it } from 'vitest';

import { documentNumberDigits, maskDocumentNumber } from './documentNumber';

describe('document number input', () => {
  it('masks the document number progressively', () => {
    expect(maskDocumentNumber('1')).toBe('1');
    expect(maskDocumentNumber('1234')).toBe('123.4');
    expect(maskDocumentNumber('1234567')).toBe('123.456.7');
    expect(maskDocumentNumber('12345678901')).toBe('123.456.789-01');
    expect(maskDocumentNumber('123.456.789-0123')).toBe('123.456.789-01');
  });

  it('keeps only the digits for the request', () => {
    expect(documentNumberDigits('123.456.789-01')).toBe('12345678901');
  });
});
