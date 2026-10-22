import { createContext } from 'react';

type Theme = 'dark' | 'light';

interface IThemeContext {
  theme: Theme;
  toggleTheme(): void;
}

export const ThemeContext = createContext({} as IThemeContext);
