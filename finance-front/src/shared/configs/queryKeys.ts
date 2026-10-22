import type { CursorDirection } from '@src/shared/dto';

type QueryKey = readonly unknown[];

const accountsKey = ['accounts'] as const;
const transfersKey = ['transfers'] as const;

export const queryKeys = {
  accounts: {
    all: accountsKey,
    page: (
      search: string,
      cursor: string | undefined,
      direction: CursorDirection
    ): QueryKey => [...accountsKey, 'page', search, cursor ?? null, direction],
    search: (search: string): QueryKey => [...accountsKey, 'search', search],
    detail: (accountId: string): QueryKey => [
      ...accountsKey,
      'detail',
      accountId,
    ],
    entriesPage: (
      accountId: string,
      cursor: string | undefined,
      direction: CursorDirection
    ): QueryKey => [
      ...accountsKey,
      'entries-page',
      accountId,
      cursor ?? null,
      direction,
    ],
  },
  transfers: {
    all: transfersKey,
    detail: (transferId: string): QueryKey => [
      ...transfersKey,
      'detail',
      transferId,
    ],
    byAccountPage: (
      accountId: string,
      cursor: string | undefined,
      direction: CursorDirection
    ): QueryKey => [
      ...transfersKey,
      'by-account-page',
      accountId,
      cursor ?? null,
      direction,
    ],
    limits: (accountId: string): QueryKey => [
      ...transfersKey,
      'limits',
      accountId,
    ],
  },
};
