import { screen, waitFor, within } from '@testing-library/react';
import { http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import type { AccountEntry } from '@src/shared/entities';
import { apiUrl } from '@src/test/apiUrl';
import {
  fakeAccountIds,
  fakeDatabase,
  problemResponse,
} from '@src/test/fake-api';
import { currencyText } from '@src/test/currencyText';
import { renderApp } from '@src/test/renderApp';
import { server } from '@src/test/server';

const unknownAccountId = '0199f3c2-0000-7000-8000-00000000ffff';

function createEntry(
  entry: Pick<AccountEntry, 'entryType' | 'amount' | 'balanceAfter'>
): AccountEntry {
  return {
    id: crypto.randomUUID(),
    accountId: fakeAccountIds.bruno,
    transferId: crypto.randomUUID(),
    counterpartAccountId: fakeAccountIds.ana,
    counterpartHolderName: 'Ana Souza',
    createdAt: new Date().toISOString(),
    ...entry,
  };
}

describe('Account details page', () => {
  beforeEach(() => {
    fakeDatabase.entries = [];
  });

  it('shows balance, overdraft in use and available amount', async () => {
    renderApp({ initialPath: `/accounts/${fakeAccountIds.bruno}` });

    expect(await screen.findByText(currencyText(-800))).toBeInTheDocument();
    expect(
      screen.getByText(`Utilizado: ${currencyText(800)}`)
    ).toBeInTheDocument();
    expect(screen.getByText(currencyText(200))).toBeInTheDocument();
  });

  it('updates the overdraft limit using the money mask', async () => {
    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.bruno}`,
    });

    await user.click(
      await screen.findByRole('button', { name: 'Alterar cheque especial' })
    );

    const overdraftLimitInput = await screen.findByLabelText(
      'Novo limite de cheque especial (R$)'
    );
    expect(overdraftLimitInput).toHaveValue('1.000,00');

    await user.clear(overdraftLimitInput);
    await user.type(overdraftLimitInput, '150000');

    expect(overdraftLimitInput).toHaveValue('1.500,00');

    await user.click(screen.getByRole('button', { name: 'Salvar limite' }));

    expect(
      await screen.findByText('Limite de cheque especial atualizado.')
    ).toBeInTheDocument();
    expect(await screen.findByText(currencyText(700))).toBeInTheDocument();
  });

  it('changes the account status', async () => {
    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.ana}`,
    });

    await user.click(
      await screen.findByRole('combobox', { name: 'Alterar status' })
    );
    await user.click(await screen.findByRole('option', { name: 'Bloqueada' }));
    await user.click(screen.getByRole('button', { name: 'Atualizar status' }));

    expect(
      await screen.findByText('Status da conta atualizado.')
    ).toBeInTheDocument();

    const statusSelect = screen.getByRole('combobox', {
      name: 'Alterar status',
    });

    await waitFor(() => {
      const statusBadges = screen
        .getAllByText('Bloqueada')
        .filter(
          (element) =>
            !statusSelect.contains(element) &&
            !(element instanceof HTMLOptionElement)
        );
      expect(statusBadges).toHaveLength(1);
    });
  });

  it('shows the statement with the balance after each entry', async () => {
    fakeDatabase.entries.push(
      createEntry({ entryType: 'Debit', amount: 800, balanceAfter: -800 }),
      createEntry({ entryType: 'Credit', amount: 1000, balanceAfter: 200 })
    );

    renderApp({ initialPath: `/accounts/${fakeAccountIds.bruno}` });

    const debitRow = await screen.findByRole('row', { name: /Débito/ });
    const creditRow = screen.getByRole('row', { name: /Crédito/ });

    expect(
      within(debitRow).getByText(`- ${currencyText(800)}`)
    ).toBeInTheDocument();
    expect(within(debitRow).getByText(currencyText(-800))).toBeInTheDocument();
    expect(
      within(creditRow).getByText(`+ ${currencyText(1000)}`)
    ).toBeInTheDocument();
    expect(within(creditRow).getByText(currencyText(200))).toBeInTheDocument();
  });

  it('shows the opening balance as the first entry of the statement', async () => {
    fakeDatabase.entries.push({
      ...createEntry({ entryType: 'Debit', amount: 800, balanceAfter: -800 }),
      transferId: null,
      counterpartAccountId: null,
      counterpartHolderName: null,
    });

    renderApp({ initialPath: `/accounts/${fakeAccountIds.bruno}` });

    const openingRow = await screen.findByRole('row', {
      name: /Saldo inicial/,
    });

    expect(
      within(openingRow).getByText(`- ${currencyText(800)}`)
    ).toBeInTheDocument();
    expect(
      within(openingRow).getByText(currencyText(-800))
    ).toBeInTheDocument();
  });

  it('paginates the statement by cursor', async () => {
    fakeDatabase.entries.push(
      ...Array.from({ length: 12 }, (_, position) =>
        createEntry({
          entryType: 'Credit',
          amount: position + 1,
          balanceAfter: -800 + position + 1,
        })
      )
    );

    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.bruno}`,
    });

    const statement = await screen.findByRole('region', { name: 'Extrato' });

    await waitFor(() =>
      expect(
        within(statement).getAllByRole('row', { name: /Crédito/ })
      ).toHaveLength(10)
    );
    expect(
      within(statement).getByRole('button', { name: 'Anterior' })
    ).toBeDisabled();

    await user.click(
      within(statement).getByRole('button', { name: 'Próxima' })
    );

    await waitFor(() =>
      expect(
        within(statement).getAllByRole('row', { name: /Crédito/ })
      ).toHaveLength(2)
    );
    expect(
      within(statement).getByRole('button', { name: 'Próxima' })
    ).toBeDisabled();

    await user.click(
      within(statement).getByRole('button', { name: 'Anterior' })
    );

    await waitFor(() =>
      expect(
        within(statement).getAllByRole('row', { name: /Crédito/ })
      ).toHaveLength(10)
    );
  });

  it('shows an empty statement message', async () => {
    renderApp({ initialPath: `/accounts/${fakeAccountIds.ana}` });

    expect(
      await screen.findByText('Nenhuma movimentação registrada.')
    ).toBeInTheDocument();
  });

  it('shows an error when the statement cannot be loaded', async () => {
    server.use(
      http.get(`${apiUrl}/accounts/:accountId/entries`, () =>
        problemResponse(500, 'UnexpectedError')
      )
    );
    renderApp({ initialPath: `/accounts/${fakeAccountIds.ana}` });

    const statement = await screen.findByRole('region', { name: 'Extrato' });

    expect(
      await within(statement).findByText('Não foi possível carregar o extrato.')
    ).toBeInTheDocument();
    expect(
      within(statement).queryByText('Nenhuma movimentação registrada.')
    ).not.toBeInTheDocument();
  });

  it('shows a not found message for unknown accounts', async () => {
    renderApp({ initialPath: `/accounts/${unknownAccountId}` });

    expect(await screen.findByText('Conta não encontrada')).toBeInTheDocument();
    expect(screen.getByText('Conta não encontrada.')).toBeInTheDocument();
  });

  it('tells the user when the account cannot be loaded', async () => {
    server.use(
      http.get(`${apiUrl}/accounts/:accountId`, () =>
        problemResponse(500, 'UnexpectedError')
      )
    );
    renderApp({ initialPath: `/accounts/${fakeAccountIds.ana}` });

    expect(
      await screen.findByText('Não foi possível carregar a conta')
    ).toBeInTheDocument();
    expect(
      screen.getByText('Ocorreu um erro inesperado. Tente novamente.')
    ).toBeInTheDocument();
    expect(screen.queryByText('Conta não encontrada')).not.toBeInTheDocument();
  });

  it('starts a new transfer from this account', async () => {
    const { user, router } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.ana}`,
    });

    await user.click(
      await screen.findByRole('link', { name: 'Nova transferência' })
    );

    expect(
      await screen.findByRole('heading', { name: 'Transferências' })
    ).toBeInTheDocument();
    expect(router.state.location.search).toMatchObject({
      sourceAccountId: fakeAccountIds.ana,
    });
  });
});
