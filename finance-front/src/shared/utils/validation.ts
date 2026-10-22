import { z } from 'zod';

import { documentNumberDigits } from './documentNumber';
import {
  hasAtMostTwoDecimals,
  maximumMoneyIntegerDigits,
  parseMoneyInput,
} from './money';

interface IMoneySchemaOptions {
  allowZero: boolean;
  requiredMessage: string;
}

const maximumMoneyValue = Number('9'.repeat(maximumMoneyIntegerDigits) + '.99');

export function moneySchema({
  allowZero,
  requiredMessage,
}: IMoneySchemaOptions): z.ZodType<number, string> {
  return z
    .string()
    .transform((maskedValue) => parseMoneyInput(maskedValue))
    .pipe(
      z
        .number({ error: requiredMessage })
        .refine((value) => !Number.isNaN(value), requiredMessage)
        .refine(
          (value) => (allowZero ? value >= 0 : value > 0),
          allowZero
            ? 'O valor não pode ser negativo.'
            : 'O valor deve ser maior que zero.'
        )
        .refine(
          (value) => value <= maximumMoneyValue,
          'Valor acima do máximo permitido.'
        )
        .refine(hasAtMostTwoDecimals, 'Use no máximo duas casas decimais.')
    );
}

export const documentNumberSchema = z
  .string()
  .transform((value) => documentNumberDigits(value))
  .pipe(z.string().length(11, 'Informe os 11 dígitos do CPF.'));
