import type { FieldError } from './FieldError';

export interface ApiProblem {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  code: string;
  transferId?: string;
  errors?: Record<string, string[]>;
  fieldErrors?: FieldError[];
}
