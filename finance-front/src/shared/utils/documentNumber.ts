export function documentNumberDigits(value: string): string {
  return value.replace(/\D/g, '').slice(0, 11);
}

export function maskDocumentNumber(value: string): string {
  const digits = documentNumberDigits(value);
  const parts = [
    digits.slice(0, 3),
    digits.slice(3, 6),
    digits.slice(6, 9),
  ].filter((part) => part !== '');
  const base = parts.join('.');
  const checkDigits = digits.slice(9, 11);
  return checkDigits === '' ? base : `${base}-${checkDigits}`;
}

export function formatDocumentNumber(documentNumber: string): string {
  return maskDocumentNumber(documentNumber);
}
