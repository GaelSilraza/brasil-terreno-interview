import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { renderApp } from '@src/test/renderApp';

describe('AppLayout', () => {
  it('redirects the root path to the accounts page', async () => {
    renderApp({ initialPath: '/' });

    expect(
      await screen.findByRole('heading', { name: 'Contas' })
    ).toBeInTheDocument();
  });

  it('navigates between pages through the sidebar', async () => {
    const { user } = renderApp({ initialPath: '/accounts' });

    await screen.findByRole('heading', { name: 'Contas' });

    await user.click(screen.getByRole('link', { name: 'Transferências' }));

    expect(
      await screen.findByRole('heading', { name: 'Transferências' })
    ).toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: 'Contas' }));

    expect(
      await screen.findByRole('heading', { name: 'Contas' })
    ).toBeInTheDocument();
  });

  it('starts in dark theme and toggles to light theme', async () => {
    const { user } = renderApp({ initialPath: '/accounts' });

    await screen.findByRole('heading', { name: 'Contas' });

    expect(document.documentElement).toHaveClass('dark');

    await user.click(screen.getByRole('button', { name: 'Tema Claro' }));

    expect(document.documentElement).not.toHaveClass('dark');
    expect(localStorage.getItem('finance:theme')).toBe('light');
  });

  it('shows the not found page for unknown routes', async () => {
    renderApp({ initialPath: '/does-not-exist' });

    expect(
      await screen.findByText('Página não encontrada')
    ).toBeInTheDocument();
  });
});
