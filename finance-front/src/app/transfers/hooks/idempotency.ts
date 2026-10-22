import { getApiProblem } from '@src/shared/utils';

export function isRetryableFailure(error: unknown): boolean {
  const problem = getApiProblem(error);
  return problem === null || problem.status >= 500;
}
