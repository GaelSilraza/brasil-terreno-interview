import type { JSX } from 'react';
import { Sun, Moon } from 'lucide-react';

import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from '@src/shared/components/ui/Sidebar';
import { useTheme } from '@src/shared/hooks/useTheme';

export function ThemeSwitch(): JSX.Element {
  const { theme, toggleTheme } = useTheme();

  const isDark = theme === 'dark';

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <SidebarMenuButton
          onClick={toggleTheme}
          tooltip={isDark ? 'Tema Claro' : 'Tema Escuro'}
        >
          {isDark ? <Sun /> : <Moon />}
          <span>{isDark ? 'Tema Claro' : 'Tema Escuro'}</span>
        </SidebarMenuButton>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
