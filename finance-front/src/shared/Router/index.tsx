import type { JSX } from 'react';
import {
  createRouter,
  RouterProvider,
  type RouterHistory,
} from '@tanstack/react-router';

import { routeTree } from './routes';

export const router = createRouter({ routeTree, defaultPreload: 'intent' });

export type AppRouter = typeof router;

export function createAppRouter(history: RouterHistory): AppRouter {
  return createRouter({ routeTree, history, defaultPreload: 'intent' });
}

declare module '@tanstack/react-router' {
  interface Register {
    router: AppRouter;
  }
}

export function Router(): JSX.Element {
  return <RouterProvider router={router} />;
}
