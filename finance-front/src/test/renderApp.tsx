import { render, type RenderResult } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { RouterProvider, createMemoryHistory } from '@tanstack/react-router';
import type { QueryClient } from '@tanstack/react-query';

import { createQueryClient } from '@src/shared/clients/queryClient';
import { AppProviders } from '@src/shared/providers/AppProviders';
import { createAppRouter, type AppRouter } from '@src/shared/Router';

interface IRenderAppOptions {
  initialPath: string;
}

export interface IRenderAppResult extends RenderResult {
  user: UserEvent;
  router: AppRouter;
  queryClient: QueryClient;
}

export function renderApp({
  initialPath,
}: IRenderAppOptions): IRenderAppResult {
  const queryClient = createQueryClient();
  const router = createAppRouter(
    createMemoryHistory({ initialEntries: [initialPath] })
  );
  const user = userEvent.setup();
  const renderResult = render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>
  );

  return { ...renderResult, user, router, queryClient };
}
