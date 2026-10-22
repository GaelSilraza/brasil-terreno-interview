import { http, HttpResponse } from 'msw';
import type { ErrorOption } from 'react-hook-form';
import { describe, expect, it } from 'vitest';

import { httpClient } from '@src/shared/clients/httpClient';
import { defaultApiErrorMessage } from '@src/shared/resources';
import { apiUrl } from '@src/test/apiUrl';
import { currencyText } from '@src/test/currencyText';
import { problemResponse, validationProblemResponse } from '@src/test/fake-api';
import { server } from '@src/test/server';

import { applyApiErrorToForm, getApiErrorMessage } from './apiErrors';
import { toResultAsync } from './patterns';

interface ITestFormValues {
  documentNumber: string;
  amount: string;
}

interface IRecordedFieldError {
  fieldName: string;
  message: string | undefined;
}

interface IRecordingSetError {
  recordedErrors: IRecordedFieldError[];
  setError: (fieldName: string, errorOption: ErrorOption) => void;
}

function createRecordingSetError(): IRecordingSetError {
  const recordedErrors: IRecordedFieldError[] = [];

  return {
    recordedErrors,
    setError: (fieldName, errorOption) => {
      recordedErrors.push({ fieldName, message: errorOption.message });
    },
  };
}

const testFieldNames: readonly (keyof ITestFormValues)[] = [
  'documentNumber',
  'amount',
];

const problemUrl = `${apiUrl}/problem`;

async function requestError(): Promise<unknown> {
  const [error] = await toResultAsync(httpClient.get(problemUrl));
  return error;
}

describe('getApiErrorMessage', () => {
  it('translates known API error codes to Portuguese', async () => {
    server.use(
      http.get(problemUrl, () => problemResponse(422, 'InsufficientFunds'))
    );

    expect(getApiErrorMessage(await requestError())).toBe(
      'Saldo e cheque especial insuficientes.'
    );
  });

  it('uses the default message for unknown codes', async () => {
    server.use(
      http.get(problemUrl, () => problemResponse(500, 'SomethingNew'))
    );

    expect(getApiErrorMessage(await requestError())).toBe(
      defaultApiErrorMessage
    );
  });

  it('uses the default message for network errors', async () => {
    server.use(http.get(problemUrl, () => HttpResponse.error()));

    expect(getApiErrorMessage(await requestError())).toBe(
      defaultApiErrorMessage
    );
  });
});

describe('applyApiErrorToForm', () => {
  it('marks only the fields returned in fieldErrors with Portuguese messages', async () => {
    server.use(
      http.get(problemUrl, () =>
        validationProblemResponse([
          { field: 'documentNumber', code: 'InvalidFormat' },
          {
            field: 'amount',
            code: 'OutOfRange',
            params: { comparisonValue: 0 },
          },
        ])
      )
    );
    const { recordedErrors, setError } = createRecordingSetError();

    applyApiErrorToForm<ITestFormValues>(
      await requestError(),
      setError,
      testFieldNames
    );

    expect(
      recordedErrors.map((recordedError) => ({
        fieldName: recordedError.fieldName,
        message: recordedError.message?.replace(/\u00a0/g, ' '),
      }))
    ).toEqual([
      { fieldName: 'documentNumber', message: 'Formato inválido.' },
      {
        fieldName: 'amount',
        message: `O valor deve ser maior que ${currencyText(0)}.`,
      },
    ]);
  });

  it('falls back to the root message for business errors', async () => {
    server.use(
      http.get(problemUrl, () => problemResponse(422, 'InsufficientFunds'))
    );
    const { recordedErrors, setError } = createRecordingSetError();

    applyApiErrorToForm<ITestFormValues>(
      await requestError(),
      setError,
      testFieldNames
    );

    expect(recordedErrors).toEqual([
      { fieldName: 'root', message: 'Saldo e cheque especial insuficientes.' },
    ]);
  });
});
