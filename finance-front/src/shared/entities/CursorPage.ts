export interface CursorPage<TItem> {
  items: TItem[];
  nextCursor: string | null;
  previousCursor: string | null;
}
