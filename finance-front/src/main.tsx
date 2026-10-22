import { StrictMode, useState, type JSX } from 'react';
import { createRoot } from 'react-dom/client';

import './index.css';
import './shared/utils/zod';
import { createQueryClient } from './shared/clients/queryClient';
import { AppProviders } from './shared/providers/AppProviders';
import { Router } from './shared/Router';

function App(): JSX.Element {
  const [queryClient] = useState(createQueryClient);

  return (
    <AppProviders queryClient={queryClient}>
      <Router />
    </AppProviders>
  );
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
