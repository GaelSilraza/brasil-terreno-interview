import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http } from 'msw';
import { describe, expect, it } from 'vitest';

import { apiUrl } from '@src/test/apiUrl';
import { problemResponse, seedManyFakeAccounts } from '@src/test/fake-api';
import { renderApp } from '@src/test/renderApp';
import { server } from '@src/test/server';

const accountOptionName = / · /;

function readAccountOptions(): HTMLElement[] {
  return screen.queryAllByRole('option', { name: accountOptionName });
}

describe('Account select field', () => {
  it('finds an account that is not on the first page by searching', async () => {
    seedManyFakeAccounts(150);
    const { user } = renderApp({ initialPath: '/transfers' });

    const sourceCombobox = await screen.findByRole('combobox', {
      name: 'Conta de origem',
    });
    await user.click(sourceCombobox);

    await waitFor(() => expect(readAccountOptions()).toHaveLength(20));
    expect(
      screen.queryByRole('option', { name: /Cliente 0140/ })
    ).not.toBeInTheDocument();

    await user.type(
      screen.getByPlaceholderText('Buscar por nome ou CPF...'),
      'Cliente 0140'
    );
    await user.click(
      await screen.findByRole('option', { name: /Cliente 0140/ })
    );

    expect(sourceCombobox).toHaveTextContent('Cliente 0140');
    expect(sourceCombobox).toHaveAttribute('aria-expanded', 'false');
  });

  it('loads the next page of accounts on demand', async () => {
    seedManyFakeAccounts(150);
    const { user } = renderApp({ initialPath: '/transfers' });

    await user.click(
      await screen.findByRole('combobox', { name: 'Conta de origem' })
    );

    await waitFor(() => expect(readAccountOptions()).toHaveLength(20));
    const firstPageLabels = readAccountOptions().map(
      (option) => option.textContent
    );

    await user.click(
      screen.getByRole('option', { name: 'Carregar mais contas' })
    );

    await waitFor(() => expect(readAccountOptions()).toHaveLength(40));
    expect(
      readAccountOptions()
        .slice(0, 20)
        .map((option) => option.textContent)
    ).toEqual(firstPageLabels);

    fireEvent.scroll(screen.getByRole('listbox', { name: 'Contas' }));

    await waitFor(() => expect(readAccountOptions()).toHaveLength(60));
  });

  it('shows the selected account even when it is outside the loaded pages', async () => {
    const seededAccounts = seedManyFakeAccounts(150);
    const lastSeededAccount = seededAccounts.at(-1);

    if (!lastSeededAccount) {
      throw new Error('No account was seeded.');
    }

    renderApp({
      initialPath: `/transfers?sourceAccountId=${lastSeededAccount.id}`,
    });

    const sourceCombobox = await screen.findByRole('combobox', {
      name: 'Conta de origem',
    });

    await waitFor(() =>
      expect(sourceCombobox).toHaveTextContent('Cliente 0150')
    );
    expect(sourceCombobox).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('limits the search text to the length the API accepts', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await user.click(
      await screen.findByRole('combobox', { name: 'Conta de origem' })
    );
    const searchInput = await screen.findByRole('combobox', {
      name: 'Buscar conta',
    });
    await user.click(searchInput);
    await user.paste('a'.repeat(120));

    expect(searchInput).toHaveValue('a'.repeat(100));
    expect(
      await screen.findByText('Nenhuma conta encontrada.')
    ).toBeInTheDocument();
  });

  it('tells the user when the account search fails', async () => {
    server.use(
      http.get(`${apiUrl}/accounts`, () =>
        problemResponse(500, 'UnexpectedError')
      )
    );
    const { user } = renderApp({ initialPath: '/transfers' });

    await user.click(
      await screen.findByRole('combobox', { name: 'Conta de origem' })
    );

    expect(
      await screen.findByText('Não foi possível buscar as contas.')
    ).toBeInTheDocument();
  });

  it('never offers the source account as destination', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    const sourceCombobox = await screen.findByRole('combobox', {
      name: 'Conta de origem',
    });
    await user.click(sourceCombobox);
    await user.click(await screen.findByRole('option', { name: /Ana Souza/ }));
    await waitFor(() => expect(sourceCombobox).toHaveTextContent('Ana Souza'));

    await user.click(
      screen.getByRole('combobox', { name: 'Conta de destino' })
    );
    const destinationList = await screen.findByRole('listbox');
    await waitFor(() =>
      expect(
        within(destinationList).getAllByRole('option', {
          name: accountOptionName,
        }).length
      ).toBeGreaterThan(0)
    );
    await user.type(
      screen.getByPlaceholderText('Buscar por nome ou CPF...'),
      'Ana'
    );

    expect(
      await screen.findByText('Nenhuma conta encontrada.')
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('option', { name: /Ana Souza/ })
    ).not.toBeInTheDocument();
  });
});
