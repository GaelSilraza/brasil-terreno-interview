import type { JSX } from 'react';
import { AlertCircleIcon } from 'lucide-react';

import {
  Alert,
  AlertDescription,
  AlertTitle,
} from '@src/shared/components/ui/Alert';

export interface IApiErrorAlertProps {
  title: string;
  message: string;
}

export function ApiErrorAlert({
  title,
  message,
}: IApiErrorAlertProps): JSX.Element {
  return (
    <Alert variant='destructive'>
      <AlertCircleIcon />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}
