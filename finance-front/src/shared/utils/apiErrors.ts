import { isAxiosError } from 'axios';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

import type { ApiProblem, FieldError } from '@src/shared/entities';
import {
  apiErrorMessages,
  defaultApiErrorMessage,
} from '@src/shared/resources/apiErrorMessages';
import { fieldErrorMessages } from '@src/shared/resources/fieldErrorMessages';

export function getApiProblem(error: unknown): ApiProblem | null {
  if (
    isAxiosError<ApiProblem>(error) &&
    typeof error.response?.data?.code === 'string'
  ) {
    return error.response.data;
  }

  return null;
}

export function isNotFoundProblem(error: unknown): boolean {
  return getApiProblem(error)?.status === 404;
}

export function getApiErrorMessage(error: unknown): string {
  const problem = getApiProblem(error);

  if (!problem) {
    return defaultApiErrorMessage;
  }

  return apiErrorMessages[problem.code] ?? defaultApiErrorMessage;
}

export function getFieldErrorMessage(fieldError: FieldError): string {
  const buildMessage =
    fieldErrorMessages[fieldError.code as keyof typeof fieldErrorMessages];
  return buildMessage
    ? buildMessage(fieldError)
    : fieldErrorMessages.InvalidValue(fieldError);
}

export function applyApiErrorToForm<TFieldValues extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<TFieldValues>,
  fieldNames: readonly Path<TFieldValues>[]
): void {
  const problem = getApiProblem(error);
  const fieldErrors = problem?.fieldErrors ?? [];

  fieldErrors.forEach((fieldError) => {
    const matchingFieldName = fieldNames.find(
      (knownFieldName) => knownFieldName === fieldError.field
    );

    if (matchingFieldName) {
      setError(matchingFieldName, {
        type: 'server',
        message: getFieldErrorMessage(fieldError),
      });
    }
  });

  const hasUnmappedFieldErrors = fieldErrors.some(
    (fieldError) =>
      !fieldNames.some((knownFieldName) => knownFieldName === fieldError.field)
  );

  if (fieldErrors.length === 0 || hasUnmappedFieldErrors) {
    setError('root', { type: 'server', message: getApiErrorMessage(error) });
  }
}
