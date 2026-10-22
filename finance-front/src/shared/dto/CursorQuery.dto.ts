export type CursorDirection = 'next' | 'previous';

export interface CursorQueryDTO {
  cursor?: string;
  direction?: CursorDirection;
  limit: number;
}

export interface AccountSearchQueryDTO extends CursorQueryDTO {
  search?: string;
}
