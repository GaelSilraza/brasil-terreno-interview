import { HttpResponse } from 'msw';

import type { FieldError, FieldErrorCode } from '@src/shared/entities';

export interface IFakeFieldError {
  field: string;
  code: FieldErrorCode;
  params?: Record<string, unknown>;
}

const englishFieldErrorMessages: Record<FieldErrorCode, string> = {
  Required: 'must not be empty.',
  InvalidFormat: 'is not in the correct format.',
  OutOfRange: 'is out of the allowed range.',
  InvalidLength: 'has an invalid length.',
  InvalidPrecision: 'must not have more than 2 decimals.',
  MustBeDifferent: 'must be different.',
  InvalidOption: 'has a value that is not allowed.',
  MustBeInFuture: 'must be in the future.',
  InvalidValue: 'is invalid.',
};

function toFieldError(fakeFieldError: IFakeFieldError): FieldError {
  return {
    field: fakeFieldError.field,
    code: fakeFieldError.code,
    message: `'${fakeFieldError.field}' ${englishFieldErrorMessages[fakeFieldError.code]}`,
    params: fakeFieldError.params ?? {},
  };
}

export function createProblemBody(
  status: number,
  code: string,
  extensions: Record<string, unknown> = {}
): Record<string, unknown> {
  return {
    type: `https://httpstatuses.io/${status}`,
    title: code,
    status,
    detail: code,
    code,
    ...extensions,
  };
}

export function createValidationProblemBody(
  fakeFieldErrors: readonly IFakeFieldError[]
): Record<string, unknown> {
  const fieldErrors = fakeFieldErrors.map(toFieldError);
  const errors: Record<string, string[]> = {};

  fieldErrors.forEach((fieldError) => {
    errors[fieldError.field] = [
      ...(errors[fieldError.field] ?? []),
      fieldError.message,
    ];
  });

  return createProblemBody(400, 'ValidationFailed', { errors, fieldErrors });
}

export function problemResponse(
  status: number,
  code: string,
  extensions: Record<string, unknown> = {}
): HttpResponse<Record<string, unknown>> {
  return HttpResponse.json(createProblemBody(status, code, extensions), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

export function validationProblemResponse(
  fakeFieldErrors: readonly IFakeFieldError[]
): HttpResponse<Record<string, unknown>> {
  return HttpResponse.json(createValidationProblemBody(fakeFieldErrors), {
    status: 400,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}
