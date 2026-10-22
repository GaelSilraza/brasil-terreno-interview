import { useState, useEffect, type JSX, type ReactNode } from 'react';

import { localStorageKeys } from '@src/shared/configs/localStorageKeys';
import { ThemeContext } from '@src/shared/contexts/ThemeContext';

type Theme = 'dark' | 'light';

interface IThemeProviderProps {
  children: ReactNode;
}

function applyTheme(theme: Theme): void {
  if (theme === 'dark') {
    return document.documentElement.classList.add('dark');
  }

  document.documentElement.classList.remove('dark');
}

export function ThemeProvider({ children }: IThemeProviderProps): JSX.Element {
  const [theme, setTheme] = useState<Theme>(() => {
    const storedTheme = localStorage.getItem(localStorageKeys.THEME);

    return storedTheme === 'light' ? 'light' : 'dark';
  });

  useEffect(() => {
    applyTheme(theme);
    localStorage.setItem(localStorageKeys.THEME, theme);
  }, [theme]);

  function toggleTheme(): void {
    setTheme((current) => (current === 'dark' ? 'light' : 'dark'));
  }

  return (
    <ThemeContext.Provider value={{ theme, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}
