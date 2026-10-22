import type { JSX } from 'react';

import { Button } from '@src/shared/components/ui/Button';

export interface ICursorPaginationControlsProps {
  hasPrevious: boolean;
  hasNext: boolean;
  isFetching: boolean;
  onPrevious: () => void;
  onNext: () => void;
}

export function CursorPaginationControls({
  hasPrevious,
  hasNext,
  isFetching,
  onPrevious,
  onNext,
}: ICursorPaginationControlsProps): JSX.Element {
  return (
    <div className='flex items-center justify-end gap-2'>
      <Button
        type='button'
        variant='outline'
        size='sm'
        disabled={!hasPrevious || isFetching}
        onClick={onPrevious}
      >
        Anterior
      </Button>
      <Button
        type='button'
        variant='outline'
        size='sm'
        disabled={!hasNext || isFetching}
        onClick={onNext}
      >
        Próxima
      </Button>
    </div>
  );
}
