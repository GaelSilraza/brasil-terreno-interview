import type { CursorDirection } from '@src/shared/dto';
import type { CursorPage } from '@src/shared/entities';

import type { IFakeFieldError } from './problems';

export interface IFakeCursorQuery {
  cursor: string | null;
  direction: CursorDirection;
  limit: number;
}

export interface IFakeCursorQueryResult {
  query: IFakeCursorQuery;
  fieldErrors: IFakeFieldError[];
}

export interface IFakeKeysetOptions<TItem> {
  sortedItems: readonly TItem[];
  query: IFakeCursorQuery;
  compareToCursor: ((item: TItem) => number) | null;
  cursorOf: (item: TItem) => string;
}

const defaultLimit = 20;
const minimumLimit = 1;
const maximumLimit = 100;

function readLimit(
  limitValue: string | null,
  fieldErrors: IFakeFieldError[]
): number {
  if (limitValue === null) {
    return defaultLimit;
  }

  if (!/^-?\d+$/.test(limitValue.trim())) {
    fieldErrors.push({ field: 'limit', code: 'InvalidValue' });
    return defaultLimit;
  }

  const limit = Number(limitValue);

  if (limit < minimumLimit || limit > maximumLimit) {
    fieldErrors.push({
      field: 'limit',
      code: 'OutOfRange',
      params: { from: minimumLimit, to: maximumLimit },
    });
    return defaultLimit;
  }

  return limit;
}

function readDirection(
  directionValue: string | null,
  fieldErrors: IFakeFieldError[]
): CursorDirection {
  if (directionValue === null) {
    return 'next';
  }

  const normalizedDirection = directionValue.toLowerCase();

  if (normalizedDirection === 'next' || normalizedDirection === 'previous') {
    return normalizedDirection;
  }

  fieldErrors.push({ field: 'direction', code: 'InvalidOption' });
  return 'next';
}

export function readCursorQuery(
  searchParams: URLSearchParams
): IFakeCursorQueryResult {
  const fieldErrors: IFakeFieldError[] = [];
  const limit = readLimit(searchParams.get('limit'), fieldErrors);
  const direction = readDirection(searchParams.get('direction'), fieldErrors);
  const cursorValue = searchParams.get('cursor');
  const cursor =
    cursorValue === null || cursorValue === '' ? null : cursorValue;

  return { query: { cursor, direction, limit }, fieldErrors };
}

export function encodeFakeCursor(cursorKey: unknown): string {
  const bytes = new TextEncoder().encode(JSON.stringify(cursorKey));
  const binary = Array.from(bytes, (byte) => String.fromCharCode(byte)).join(
    ''
  );

  return btoa(binary)
    .replace(/\+/g, '-')
    .split('/')
    .join('_')
    .replace(/=+$/, '');
}

export function decodeFakeCursor(cursor: string): unknown {
  try {
    const base64 = cursor.replace(/-/g, '+').replace(/_/g, '/');
    const padding = '='.repeat((4 - (base64.length % 4)) % 4);
    const binary = atob(base64 + padding);
    const bytes = Uint8Array.from(binary, (character) =>
      character.charCodeAt(0)
    );
    const decodedValue: unknown = JSON.parse(
      new TextDecoder('utf-8', { fatal: true }).decode(bytes)
    );
    return decodedValue;
  } catch {
    return null;
  }
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export function paginateByKeyset<TItem>({
  sortedItems,
  query,
  compareToCursor,
  cursorOf,
}: IFakeKeysetOptions<TItem>): CursorPage<TItem> {
  if (compareToCursor === null) {
    const hasNext = sortedItems.length > query.limit;
    const items = sortedItems.slice(0, query.limit);
    const lastItem = items.at(-1);

    return {
      items,
      nextCursor: hasNext && lastItem !== undefined ? cursorOf(lastItem) : null,
      previousCursor: null,
    };
  }

  if (query.direction === 'previous') {
    const itemsBeforeCursor = sortedItems.filter(
      (item) => compareToCursor(item) < 0
    );
    const hasPrevious = itemsBeforeCursor.length > query.limit;
    const items = itemsBeforeCursor.slice(-query.limit);
    const firstItem = items.at(0);
    const lastItem = items.at(-1);

    return {
      items,
      nextCursor: lastItem !== undefined ? cursorOf(lastItem) : null,
      previousCursor:
        hasPrevious && firstItem !== undefined ? cursorOf(firstItem) : null,
    };
  }

  const itemsAfterCursor = sortedItems.filter(
    (item) => compareToCursor(item) > 0
  );
  const hasNext = itemsAfterCursor.length > query.limit;
  const items = itemsAfterCursor.slice(0, query.limit);
  const firstItem = items.at(0);
  const lastItem = items.at(-1);

  return {
    items,
    nextCursor: hasNext && lastItem !== undefined ? cursorOf(lastItem) : null,
    previousCursor: firstItem !== undefined ? cursorOf(firstItem) : null,
  };
}
