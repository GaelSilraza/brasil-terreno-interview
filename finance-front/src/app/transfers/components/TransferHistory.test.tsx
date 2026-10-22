import { screen, waitFor, within } from '@testing-library/react';
import { http } from 'msw';
import { describe, expect, it } from 'vitest';

import { apiUrl } from '@src/test/apiUrl';
import {
  addFakeTransfer,
  fakeAccountIds,
  problemResponse,
} from '@src/test/fake-api';
import { currencyText } from '@src/test/currencyText';
import { renderApp } from '@src/test/renderApp';
import { server } from '@src/test/server';

function addAnaTransfers(): void {
  addFakeTransfer({
    sourceAccountId: fakeAccountIds.ana,
    destinationAccountId: fakeAccountIds.carla,
    amount: 100,
    type: 'Immediate',
    status: 'Completed',
  });
  addFakeTransfer({
    sourceAccountId: fakeAccountIds.bruno,
    destinationAccountId: fakeAccountIds.ana,
    amount: 50,
    type: 'Immediate',
    status: 'Failed',
    failureCode: 'InsufficientFunds',
  });
}

const historyPageSize = 10;

function addManyAnaTransfers(count: number): void {
  Array.from({ length: count }, (_, position) => {
    const isSent = position % 2 === 0;

    return addFakeTransfer({
      sourceAccountId: isSent ? fakeAccountIds.ana : fakeAccountIds.bruno,
      destinationAccountId: isSent ? fakeAccountIds.carla : fakeAccountIds.ana,
      amount: position + 1,
      type: 'Immediate',
      status: 'Completed',
    });
  });
}

function findHistoryRegion(): Promise<HTMLElement> {
  return screen.findByRole('region', { name: 'Transferências' });
}

function visibleTransferLinks(historyRegion: HTMLElement): string[] {
  return within(historyRegion)
    .queryAllByRole('link', { name: 'Detalhes' })
    .map((link) => link.getAttribute('href') ?? '');
}

async function waitForPageChange(
  historyRegion: HTMLElement,
  previousLinks: readonly string[]
): Promise<string[]> {
  await waitFor(() => {
    const currentLinks = visibleTransferLinks(historyRegion);
    expect(currentLinks.length).toBeGreaterThan(0);
    expect(currentLinks).not.toEqual(previousLinks);
  });

  return visibleTransferLinks(historyRegion);
}

describe('Transfer history on the account page', () => {
  it('lists sent and received transfers of the account', async () => {
    addAnaTransfers();
    renderApp({ initialPath: `/accounts/${fakeAccountIds.ana}` });

    const historyRegion = await findHistoryRegion();
    const sentRow = await within(historyRegion).findByRole('row', {
      name: /Carla Mendes/,
    });
    const receivedRow = within(historyRegion).getByRole('row', {
      name: /Bruno Lima/,
    });

    expect(within(sentRow).getByText('Concluída')).toBeInTheDocument();
    expect(
      within(sentRow).getByText(`- ${currencyText(100)}`)
    ).toBeInTheDocument();
    expect(within(receivedRow).getByText('Falhou')).toBeInTheDocument();
    expect(
      within(receivedRow).getByText(`+ ${currencyText(50)}`)
    ).toBeInTheDocument();
  });

  it('opens a transfer from the history', async () => {
    addAnaTransfers();
    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.ana}`,
    });

    const historyRegion = await findHistoryRegion();
    const sentRow = await within(historyRegion).findByRole('row', {
      name: /Carla Mendes/,
    });
    await user.click(within(sentRow).getByRole('link', { name: 'Detalhes' }));

    expect(
      await screen.findByRole('heading', { name: 'Detalhes da transferência' })
    ).toBeInTheDocument();
  });

  it('shows an empty history message', async () => {
    renderApp({ initialPath: `/accounts/${fakeAccountIds.ana}` });

    const historyRegion = await findHistoryRegion();

    expect(
      await within(historyRegion).findByText(
        'Nenhuma transferência encontrada.'
      )
    ).toBeInTheDocument();
  });

  it('shows an error when the history cannot be loaded', async () => {
    server.use(
      http.get(`${apiUrl}/transfers`, () =>
        problemResponse(500, 'UnexpectedError')
      )
    );
    renderApp({ initialPath: `/accounts/${fakeAccountIds.ana}` });

    const historyRegion = await findHistoryRegion();

    expect(
      await within(historyRegion).findByText(
        'Não foi possível carregar as transferências.'
      )
    ).toBeInTheDocument();
    expect(
      within(historyRegion).queryByText('Nenhuma transferência encontrada.')
    ).not.toBeInTheDocument();
  });

  it('paginates the account history by cursor', async () => {
    addManyAnaTransfers(23);
    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.ana}`,
    });

    const historyRegion = await findHistoryRegion();
    await waitFor(() =>
      expect(visibleTransferLinks(historyRegion)).toHaveLength(historyPageSize)
    );
    const firstPageLinks = visibleTransferLinks(historyRegion);
    const nextButton = within(historyRegion).getByRole('button', {
      name: 'Próxima',
    });
    const previousButton = within(historyRegion).getByRole('button', {
      name: 'Anterior',
    });

    expect(previousButton).toBeDisabled();

    await user.click(nextButton);
    const secondPageLinks = await waitForPageChange(
      historyRegion,
      firstPageLinks
    );

    expect(secondPageLinks).toHaveLength(historyPageSize);

    await waitFor(() => expect(nextButton).toBeEnabled());
    await user.click(nextButton);
    const thirdPageLinks = await waitForPageChange(
      historyRegion,
      secondPageLinks
    );

    expect(thirdPageLinks).toHaveLength(3);
    await waitFor(() => expect(previousButton).toBeEnabled());
    expect(nextButton).toBeDisabled();
    expect(
      new Set([...firstPageLinks, ...secondPageLinks, ...thirdPageLinks]).size
    ).toBe(23);

    await user.click(previousButton);
    const previousPageLinks = await waitForPageChange(
      historyRegion,
      thirdPageLinks
    );

    expect(previousPageLinks).toHaveLength(historyPageSize);
    expect(previousPageLinks).toEqual(secondPageLinks);
  });
});
