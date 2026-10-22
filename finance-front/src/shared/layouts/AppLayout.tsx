import type { JSX } from 'react';
import { Outlet } from '@tanstack/react-router';

import { AppSidebar } from '@src/shared/components/AppSidebar';
import {
  SidebarInset,
  SidebarProvider,
  SidebarTrigger,
} from '@src/shared/components/ui/Sidebar';
import { useIsMobile } from '@src/shared/hooks/useIsMobile';

export function AppLayout(): JSX.Element {
  const isMobile = useIsMobile();

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset className='overflow-x-auto'>
        {isMobile && (
          <header className='flex h-16 shrink-0 items-center gap-2 border-b px-4'>
            <SidebarTrigger className='ml-1' />
          </header>
        )}
        <main className='flex flex-col gap-6 p-6'>
          <Outlet />
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
