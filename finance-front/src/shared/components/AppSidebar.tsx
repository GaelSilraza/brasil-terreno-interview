import type { JSX } from 'react';
import { Wallet } from 'lucide-react';

import { NavContent } from '@src/shared/components/NavContent';
import { ThemeSwitch } from '@src/shared/components/ThemeSwitch';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarRail,
  SidebarTrigger,
  useSidebar,
} from '@src/shared/components/ui/Sidebar';
import { navigationConfig } from '@src/shared/configs/navigationConfig';
import { cn } from '@src/shared/lib/utils';

export function AppSidebar(): JSX.Element {
  const { state } = useSidebar();
  const isCollapsed = state === 'collapsed';

  return (
    <Sidebar collapsible='icon'>
      <SidebarHeader
        className={cn(
          'flex items-center gap-2',
          isCollapsed ? 'flex-col' : 'flex-row justify-between'
        )}
      >
        <div className='flex items-center gap-2 px-2 font-semibold'>
          <Wallet className='size-5 text-primary' />
          {!isCollapsed && <span>Finance</span>}
        </div>
        <SidebarTrigger className='cursor-pointer' />
      </SidebarHeader>
      <SidebarContent>
        {navigationConfig.map((navigationGroup) => (
          <NavContent
            key={navigationGroup.groupName}
            navigationGroup={navigationGroup}
          />
        ))}
      </SidebarContent>
      <SidebarFooter>
        <ThemeSwitch />
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  );
}
