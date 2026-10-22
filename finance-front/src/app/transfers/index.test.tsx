import { screen, waitFor, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';

import { defaultApiErrorMessage } from '@src/shared/resources';
import { toDateTimeLocalValue } from '@src/shared/utils';
import { apiUrl } from '@src/test/apiUrl';
import {
  addFakeTransfer,
  fakeAccountIds,
  fakeDatabase,
  findFakeAccount,
  problemResponse,
  seedManyFakeAccounts,
  validationProblemResponse,
} from '@src/test/fake-api';
import { renderApp } from '@src/test/renderApp';
import { server } from '@src/test/server';

const oneHourInMilliseconds = 60 * 60 * 1000;

async function findToast(message: string): Promise<HTMLElement> {
  const notifications = screen.getByRole('region', { name: /Notificações/ });
  return within(notifications).findByText(message);
}

async function selectAccount(
  user: UserEvent,
  fieldLabel: string,
  holderName: string
): Promise<void> {
  const combobox = await screen.findByRole('combobox', { name: fieldLabel });
  await waitFor(() => expect(combobox).toBeEnabled());
  await user.click(combobox);
  await user.type(
    await screen.findByPlaceholderText('Buscar por nome ou CPF...'),
    holderName
  );
  const holderNamePattern = new RegExp(holderName);
  await waitFor(() => {
    const accountOptions = screen.getAllByRole('option', { name: /·/ });
    expect(accountOptions.length).toBeGreaterThan(0);
    expect(
      accountOptions.every((option) =>
        holderNamePattern.test(option.textContent ?? '')
      )
    ).toBe(true);
  });
  await user.click(screen.getByRole('option', { name: holderNamePattern }));
  await waitFor(() => expect(combobox).toHaveTextContent(holderName));
}

async function fillImmediateTransfer(
  user: UserEvent,
  amountInCents: string
): Promise<void> {
  await selectAccount(user, 'Conta de origem', 'Ana Souza');
  await selectAccount(user, 'Conta de destino', 'Carla Mendes');
  await user.type(screen.getByLabelText('Valor (R$)'), amountInCents);
}

function captureSentIdempotencyKeys(): (string | null)[] {
  const sentIdempotencyKeys: (string | null)[] = [];

  server.events.on('request:start', ({ request }) => {
    if (request.method === 'POST' && request.url.endsWith('/transfers')) {
      sentIdempotencyKeys.push(request.headers.get('Idempotency-Key'));
    }
  });

  return sentIdempotencyKeys;
}

async function fillScheduledTransfer(
  user: UserEvent,
  scheduledDate: Date
): Promise<void> {
  await user.click(await screen.findByRole('tab', { name: 'Agendada' }));
  await selectAccount(user, 'Conta de origem', 'Ana Souza');
  await selectAccount(user, 'Conta de destino', 'Carla Mendes');
  await user.type(screen.getByLabelText('Valor (R$)'), '5000');
  const dateInput = screen.getByLabelText('Data e hora');
  await user.clear(dateInput);
  await user.type(dateInput, toDateTimeLocalValue(scheduledDate));
}

describe('Transfers page', () => {
  afterEach(() => {
    server.events.removeAllListeners();
  });

  it('completes an immediate transfer and opens its details', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillImmediateTransfer(user, '10000');

    expect(screen.getByLabelText('Valor (R$)')).toHaveValue('100,00');

    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(await findToast('Transferência concluída.')).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { name: 'Detalhes da transferência' })
    ).toBeInTheDocument();
    expect(await screen.findByText('Concluída')).toBeInTheDocument();
    expect(findFakeAccount(fakeAccountIds.ana)?.balance).toBe(400);
  });

  it('shows the rejection reason when balance and overdraft are not enough', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillImmediateTransfer(user, '200000');
    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(
      await findToast('Saldo e cheque especial insuficientes.')
    ).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { name: 'Detalhes da transferência' })
    ).toBeInTheDocument();
    expect(await screen.findByText('Falhou')).toBeInTheDocument();
    expect(screen.getByText('Motivo da falha')).toBeInTheDocument();
  });

  it('shows the hourly limit message returned by the API', async () => {
    const rejectedTransfer = addFakeTransfer({
      sourceAccountId: fakeAccountIds.ana,
      destinationAccountId: fakeAccountIds.carla,
      amount: 100,
      type: 'Immediate',
      status: 'Failed',
      failureCode: 'HourlyAmountLimitExceeded',
    });
    server.use(
      http.post(`${apiUrl}/transfers`, () =>
        problemResponse(422, 'HourlyAmountLimitExceeded', {
          transferId: rejectedTransfer.id,
        })
      )
    );
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillImmediateTransfer(user, '10000');
    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(
      await findToast('Limite de valor por hora excedido.')
    ).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { name: 'Detalhes da transferência' })
    ).toBeInTheDocument();
  });

  it('does not offer the source account as destination', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await selectAccount(user, 'Conta de origem', 'Ana Souza');
    await user.click(
      screen.getByRole('combobox', { name: 'Conta de destino' })
    );

    expect(
      await screen.findByRole('option', { name: /Carla Mendes/ })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('option', { name: /Ana Souza/ })
    ).not.toBeInTheDocument();
  });

  it('validates the amount before sending anything', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillImmediateTransfer(user, '0');
    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(
      await screen.findByText('O valor deve ser maior que zero.')
    ).toBeInTheDocument();
    expect(fakeDatabase.transfers).toHaveLength(0);
  });

  it('requires different source and destination accounts', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillImmediateTransfer(user, '1000');
    await selectAccount(user, 'Conta de origem', 'Carla Mendes');
    await user.click(
      screen.getByRole('combobox', { name: 'Conta de destino' })
    );

    expect(
      await screen.findByRole('option', { name: /Ana Souza/ })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('option', { name: /Carla Mendes/ })
    ).not.toBeInTheDocument();

    await user.keyboard('{Escape}');
    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    const destinationCombobox = screen.getByRole('combobox', {
      name: 'Conta de destino',
    });

    await waitFor(() =>
      expect(destinationCombobox).toHaveAccessibleDescription(
        'A conta de origem e a de destino devem ser diferentes.'
      )
    );
    expect(destinationCombobox).toHaveAttribute('aria-invalid', 'true');
    expect(fakeDatabase.transfers).toHaveLength(0);
  });

  it('reuses the idempotency key when the network fails and the user retries', async () => {
    const sentIdempotencyKeys = captureSentIdempotencyKeys();
    server.use(
      http.post(`${apiUrl}/transfers`, () => HttpResponse.error(), {
        once: true,
      })
    );
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillImmediateTransfer(user, '10000');
    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(await findToast(defaultApiErrorMessage)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(await findToast('Transferência concluída.')).toBeInTheDocument();
    expect(sentIdempotencyKeys).toHaveLength(2);
    expect(sentIdempotencyKeys[0]).not.toBeNull();
    expect(sentIdempotencyKeys[1]).toBe(sentIdempotencyKeys[0]);
    expect(fakeDatabase.transfers).toHaveLength(1);
  });

  it('uses a new idempotency key for each completed transfer', async () => {
    const sentIdempotencyKeys = captureSentIdempotencyKeys();
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillImmediateTransfer(user, '1000');
    await user.click(screen.getByRole('button', { name: 'Transferir' }));
    await screen.findByRole('heading', { name: 'Detalhes da transferência' });

    await user.click(screen.getByRole('link', { name: 'Transferências' }));
    await screen.findByRole('heading', { name: 'Transferências' });
    await fillImmediateTransfer(user, '2000');
    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    await waitFor(() => expect(fakeDatabase.transfers).toHaveLength(2));
    expect(sentIdempotencyKeys).toHaveLength(2);
    expect(sentIdempotencyKeys[0]).not.toBeNull();
    expect(sentIdempotencyKeys[1]).not.toBeNull();
    expect(sentIdempotencyKeys[1]).not.toBe(sentIdempotencyKeys[0]);
  });

  it('preselects the source account from the address', async () => {
    renderApp({
      initialPath: `/transfers?sourceAccountId=${fakeAccountIds.carla}`,
    });

    const sourceCombobox = await screen.findByRole('combobox', {
      name: 'Conta de origem',
    });

    await waitFor(() =>
      expect(sourceCombobox).toHaveTextContent(/Carla Mendes/)
    );
  });

  it('schedules a transfer for a future date', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillScheduledTransfer(
      user,
      new Date(Date.now() + 2 * oneHourInMilliseconds)
    );
    await user.click(screen.getByRole('button', { name: 'Agendar' }));

    expect(await findToast('Transferência agendada.')).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { name: 'Detalhes da transferência' })
    ).toBeInTheDocument();
    expect(await screen.findAllByText('Agendada')).toHaveLength(2);
    expect(
      screen.getByRole('button', { name: 'Cancelar agendamento' })
    ).toBeInTheDocument();
    expect(fakeDatabase.transfers[0]?.status).toBe('Scheduled');
  });

  it('rejects a schedule date in the past', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillScheduledTransfer(
      user,
      new Date(Date.now() - oneHourInMilliseconds)
    );
    await user.click(screen.getByRole('button', { name: 'Agendar' }));

    expect(
      await screen.findByText('A data deve ser futura.')
    ).toBeInTheDocument();
    expect(fakeDatabase.transfers).toHaveLength(0);
  });

  it('finds a transfer by its code', async () => {
    const transfer = addFakeTransfer({
      sourceAccountId: fakeAccountIds.ana,
      destinationAccountId: fakeAccountIds.carla,
      amount: 100,
      type: 'Immediate',
      status: 'Completed',
    });
    const { user } = renderApp({ initialPath: '/transfers' });

    await user.type(
      await screen.findByLabelText('Código da transferência'),
      transfer.id
    );
    await user.click(screen.getByRole('button', { name: 'Consultar' }));

    expect(
      await screen.findByRole('heading', { name: 'Detalhes da transferência' })
    ).toBeInTheDocument();
    expect(await screen.findByText(transfer.id)).toBeInTheDocument();
  });

  it('finds a transfer by a code pasted with surrounding spaces', async () => {
    const transfer = addFakeTransfer({
      sourceAccountId: fakeAccountIds.ana,
      destinationAccountId: fakeAccountIds.carla,
      amount: 100,
      type: 'Immediate',
      status: 'Completed',
    });
    const { user } = renderApp({ initialPath: '/transfers' });

    await user.click(await screen.findByLabelText('Código da transferência'));
    await user.paste(`  ${transfer.id}  `);
    await user.click(screen.getByRole('button', { name: 'Consultar' }));

    expect(await screen.findByText(transfer.id)).toBeInTheDocument();
  });

  it('rejects an invalid transfer code', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await user.type(
      await screen.findByLabelText('Código da transferência'),
      'abc'
    );
    await user.click(screen.getByRole('button', { name: 'Consultar' }));

    expect(
      await screen.findByText('Informe um código de transferência válido.')
    ).toBeInTheDocument();
  });

  it('shows the amount with two decimals while typing', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });
    const amountInput = await screen.findByLabelText('Valor (R$)');

    expect(amountInput).toHaveValue('0,00');

    await selectAccount(user, 'Conta de origem', 'Carla Mendes');
    await selectAccount(user, 'Conta de destino', 'Ana Souza');
    await user.type(amountInput, '150075');

    expect(amountInput).toHaveValue('1.500,75');

    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(await findToast('Transferência concluída.')).toBeInTheDocument();
    expect(fakeDatabase.transfers).toHaveLength(1);
    expect(fakeDatabase.transfers[0]?.amount).toBe(1500.75);
  });

  it('rejects a zero amount before sending', async () => {
    const { user } = renderApp({ initialPath: '/transfers' });

    await selectAccount(user, 'Conta de origem', 'Ana Souza');
    await selectAccount(user, 'Conta de destino', 'Carla Mendes');

    expect(screen.getByLabelText('Valor (R$)')).toHaveValue('0,00');

    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(
      await screen.findByText('O valor deve ser maior que zero.')
    ).toBeInTheDocument();
    expect(fakeDatabase.transfers).toHaveLength(0);
  });

  it('shows the server field error for a past schedule date', async () => {
    server.use(
      http.post(`${apiUrl}/transfers/scheduled`, () =>
        validationProblemResponse([
          { field: 'scheduledFor', code: 'MustBeInFuture' },
        ])
      )
    );
    const { user } = renderApp({ initialPath: '/transfers' });

    await fillScheduledTransfer(
      user,
      new Date(Date.now() + 2 * oneHourInMilliseconds)
    );
    await user.click(screen.getByRole('button', { name: 'Agendar' }));

    const dateInput = screen.getByLabelText('Data e hora');

    await waitFor(() =>
      expect(dateInput).toHaveAccessibleDescription('A data deve ser futura.')
    );
    expect(dateInput).toHaveAttribute('aria-invalid', 'true');
    expect(fakeDatabase.transfers).toHaveLength(0);
  });

  it('transfers to an account found by searching beyond the first page', async () => {
    const seededAccounts = seedManyFakeAccounts(120);
    const destinationAccount = seededAccounts[118];
    const { user } = renderApp({ initialPath: '/transfers' });

    await selectAccount(user, 'Conta de origem', 'Cliente 0001');
    await selectAccount(user, 'Conta de destino', 'Cliente 0119');
    await user.type(screen.getByLabelText('Valor (R$)'), '5000');

    expect(screen.getByLabelText('Valor (R$)')).toHaveValue('50,00');

    await user.click(screen.getByRole('button', { name: 'Transferir' }));

    expect(
      await screen.findByRole('heading', { name: 'Detalhes da transferência' })
    ).toBeInTheDocument();
    expect(await screen.findByText('Concluída')).toBeInTheDocument();
    expect(destinationAccount?.holderName).toBe('Cliente 0119');
    expect(findFakeAccount(destinationAccount?.id ?? '')?.balance).toBe(1050);
  });

  it('aligns the lookup card with the transfer form card', async () => {
    renderApp({ initialPath: '/transfers' });

    const lookupCard = (
      await screen.findByText('Consultar transferência')
    ).closest('[data-slot="card"]');
    const formCard = screen
      .getByText('Transferência imediata')
      .closest('[data-slot="card"]');
    const gridContainer = lookupCard?.closest('.grid');

    expect(lookupCard).not.toBeNull();
    expect(formCard).not.toBeNull();
    expect(gridContainer).toBeInstanceOf(HTMLElement);
    expect(formCard?.closest('.grid')).toBe(gridContainer);
    expect(lookupCard?.parentElement).toBe(gridContainer);
    expect(gridContainer?.contains(screen.getByRole('tablist'))).toBe(false);
  });
});
