import { useCallback, useState } from 'react';

import type { CursorDirection } from '@src/shared/dto';

export interface ICursorState {
  cursor?: string;
  direction: CursorDirection;
}

export interface IUseCursorPaginationResult {
  cursorState: ICursorState;
  goToNextPage: (nextCursor: string | null) => void;
  goToPreviousPage: (previousCursor: string | null) => void;
}

const firstPageCursorState: ICursorState = { direction: 'next' };

export function useCursorPagination(): IUseCursorPaginationResult {
  const [cursorState, setCursorState] =
    useState<ICursorState>(firstPageCursorState);

  const goToNextPage = useCallback((nextCursor: string | null): void => {
    if (nextCursor !== null) {
      setCursorState({ cursor: nextCursor, direction: 'next' });
    }
  }, []);

  const goToPreviousPage = useCallback(
    (previousCursor: string | null): void => {
      if (previousCursor !== null) {
        setCursorState({ cursor: previousCursor, direction: 'previous' });
      }
    },
    []
  );

  return { cursorState, goToNextPage, goToPreviousPage };
}
