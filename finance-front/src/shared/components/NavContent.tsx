import type { JSX } from 'react';
import { Link, useRouterState } from '@tanstack/react-router';

import {
  SidebarGroup,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from '@src/shared/components/ui/Sidebar';
import type { INavigationGroup } from '@src/shared/configs/navigationConfig';

interface INavContentProps {
  navigationGroup: INavigationGroup;
}

export function NavContent({ navigationGroup }: INavContentProps): JSX.Element {
  const pathname = useRouterState({
    select: (routerState) => routerState.location.pathname,
  });

  return (
    <SidebarGroup>
      <SidebarGroupLabel>{navigationGroup.groupName}</SidebarGroupLabel>
      <SidebarMenu>
        {navigationGroup.contents.map((navigationContent) => (
          <SidebarMenuItem key={navigationContent.url}>
            <SidebarMenuButton
              asChild
              isActive={pathname.startsWith(navigationContent.url)}
              tooltip={navigationContent.name}
            >
              <Link to={navigationContent.url}>
                <navigationContent.icon />
                <span>{navigationContent.name}</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        ))}
      </SidebarMenu>
    </SidebarGroup>
  );
}
