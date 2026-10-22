import type { FieldError } from '@src/shared/entities';
import { formatCurrency } from '@src/shared/utils/formatCurrency';

export const fieldErrorCodes = [
  'Required',
  'InvalidFormat',
  'OutOfRange',
  'InvalidLength',
  'InvalidPrecision',
  'MustBeDifferent',
  'InvalidOption',
  'MustBeInFuture',
  'InvalidValue',
] as const;

type FieldErrorMessageBuilder = (fieldError: FieldError) => string;

function numberParameter(
  fieldError: FieldError,
  parameterName: string
): number | null {
  const parameterValue = fieldError.params[parameterName];
  return typeof parameterValue === 'number' ? parameterValue : null;
}

export const fieldErrorMessages: Record<
  (typeof fieldErrorCodes)[number],
  FieldErrorMessageBuilder
> = {
  Required: () => 'Campo obrigatório.',
  InvalidFormat: () => 'Formato inválido.',
  OutOfRange: (fieldError) => {
    const from = numberParameter(fieldError, 'from');
    const to = numberParameter(fieldError, 'to');
    const comparisonValue = numberParameter(fieldError, 'comparisonValue');

    if (from !== null && to !== null) {
      return `Informe um valor entre ${from} e ${to}.`;
    }

    if (comparisonValue !== null) {
      const formattedValue = formatCurrency(comparisonValue);
      const comparisonTexts: Record<string, string> = {
        greaterThan: `O valor deve ser maior que ${formattedValue}.`,
        greaterThanOrEqual: `O valor deve ser maior ou igual a ${formattedValue}.`,
        lessThan: `O valor deve ser menor que ${formattedValue}.`,
        lessThanOrEqual: `O valor deve ser menor ou igual a ${formattedValue}.`,
      };
      const comparison = fieldError.params.comparison;

      return typeof comparison === 'string' && comparisonTexts[comparison]
        ? comparisonTexts[comparison]
        : `O valor deve ser maior que ${formattedValue}.`;
    }

    return 'Valor fora do intervalo permitido.';
  },
  InvalidLength: () => 'Tamanho inválido.',
  InvalidPrecision: () => 'Use no máximo duas casas decimais.',
  MustBeDifferent: () =>
    'A conta de origem e a de destino devem ser diferentes.',
  InvalidOption: () => 'Opção inválida.',
  MustBeInFuture: () => 'A data deve ser futura.',
  InvalidValue: () => 'Valor inválido.',
};
