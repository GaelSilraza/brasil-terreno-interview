import type { fieldErrorCodes } from '@src/shared/resources/fieldErrorMessages';

export type FieldErrorCode = (typeof fieldErrorCodes)[number];

export interface FieldError {
  field: string;
  code: FieldErrorCode;
  message: string;
  params: Record<string, unknown>;
}
