import { useContext, type ContextType } from 'react';

import { ThemeContext } from '@src/shared/contexts/ThemeContext';

export function useTheme(): ContextType<typeof ThemeContext> {
  return useContext(ThemeContext);
}
