import type { JSX, ReactNode } from 'react';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';

import { Toaster } from '@src/shared/components/ui/Sonner';
import { useTheme } from '@src/shared/hooks/useTheme';
import { ThemeProvider } from '@src/shared/providers/ThemeProvider';

interface IAppProvidersProps {
  queryClient: QueryClient;
  children: ReactNode;
}

function ThemedToaster(): JSX.Element {
  const { theme } = useTheme();

  return (
    <Toaster
      theme={theme}
      closeButton
      richColors
      dir='ltr'
      containerAriaLabel='Notificações'
      toastOptions={{ closeButtonAriaLabel: 'Fechar notificação' }}
    />
  );
}

const shouldRenderDevtools =
  import.meta.env.DEV && import.meta.env.MODE !== 'test';

export function AppProviders({
  queryClient,
  children,
}: IAppProvidersProps): JSX.Element {
  return (
    <ThemeProvider>
      <ThemedToaster />
      <QueryClientProvider client={queryClient}>
        {children}
        {shouldRenderDevtools && <ReactQueryDevtools />}
      </QueryClientProvider>
    </ThemeProvider>
  );
}
