import { createRootRoute, createRoute, redirect } from '@tanstack/react-router';
import { z } from 'zod';

import { Accounts } from '@src/app/accounts';
import { AccountDetails } from '@src/app/accounts/components/account-details';
import { Transfers } from '@src/app/transfers';
import { TransferDetails } from '@src/app/transfers/components/transfer-details';
import { NotFound } from '@src/shared/components/NotFound';
import { AppLayout } from '@src/shared/layouts/AppLayout';

const transfersSearchSchema = z.object({
  tab: z.enum(['immediate', 'scheduled']).optional().catch(undefined),
  sourceAccountId: z.string().optional().catch(undefined),
});

export type TransfersSearch = z.infer<typeof transfersSearchSchema>;

const rootRoute = createRootRoute({
  component: AppLayout,
  notFoundComponent: NotFound,
});

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  beforeLoad: () => {
    throw redirect({ to: '/accounts' });
  },
});

const accountsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/accounts',
  component: Accounts,
});

const accountDetailsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/accounts/$accountId',
  component: AccountDetails,
});

const transfersRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/transfers',
  validateSearch: (search: Record<string, unknown>): TransfersSearch =>
    transfersSearchSchema.parse(search),
  component: Transfers,
});

const transferDetailsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/transfers/$transferId',
  component: TransferDetails,
});

export const routeTree = rootRoute.addChildren([
  indexRoute,
  accountsRoute,
  accountDetailsRoute,
  transfersRoute,
  transferDetailsRoute,
]);
