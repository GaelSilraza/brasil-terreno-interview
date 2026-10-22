import { screen, waitFor, within } from '@testing-library/react';
import { http } from 'msw';
import { describe, expect, it } from 'vitest';

import { queryKeys } from '@src/shared/configs/queryKeys';
import { apiUrl } from '@src/test/apiUrl';
import { currencyText } from '@src/test/currencyText';
import {
  addFakeTransfer,
  fakeAccountIds,
  fakeDatabase,
  problemResponse,
  setFakeTransferStatus,
} from '@src/test/fake-api';
import { renderApp } from '@src/test/renderApp';
import { server } from '@src/test/server';

const oneDayInMilliseconds = 24 * 60 * 60 * 1000;

async function findToast(message: string): Promise<HTMLElement> {
  const notifications = screen.getByRole('region', { name: /Notificações/ });
  return within(notifications).findByText(message);
}

function addScheduledTransfer(
  millisecondsFromNow: number = oneDayInMilliseconds
): string {
  return addFakeTransfer({
    sourceAccountId: fakeAccountIds.ana,
    destinationAccountId: fakeAccountIds.carla,
    amount: 100,
    type: 'Scheduled',
    status: 'Scheduled',
    scheduledFor: new Date(Date.now() + millisecondsFromNow).toISOString(),
  }).id;
}

async function findCancelButton(): Promise<HTMLElement> {
  await screen.findByRole('heading', { name: 'Detalhes da transferência' });
  return screen.findByRole('button', { name: 'Cancelar agendamento' });
}

describe('Transfer details page', () => {
  it('cancels a scheduled transfer after confirmation', async () => {
    const transferId = addScheduledTransfer();
    const { user } = renderApp({ initialPath: `/transfers/${transferId}` });

    await user.click(await findCancelButton());

    expect(
      await screen.findByRole('dialog', {
        name: 'Cancelar transferência agendada?',
      })
    ).toBeInTheDocument();
    expect(
      screen.getByText('A transferência não será executada.')
    ).toBeInTheDocument();

    await user.click(
      screen.getByRole('button', { name: 'Confirmar cancelamento' })
    );

    expect(await findToast('Transferência cancelada.')).toBeInTheDocument();
    expect(await screen.findByText('Cancelada')).toBeInTheDocument();
    await waitFor(() =>
      expect(
        screen.queryByRole('button', { name: 'Cancelar agendamento' })
      ).not.toBeInTheDocument()
    );
    expect(fakeDatabase.transfers[0]?.status).toBe('Cancelled');
  });

  it('refreshes a scheduled transfer when its date arrives', async () => {
    const transferId = addScheduledTransfer(1000);
    renderApp({ initialPath: `/transfers/${transferId}` });

    await findCancelButton();

    expect(screen.queryByText('Atualizando status...')).not.toBeInTheDocument();

    setFakeTransferStatus(transferId, 'Completed');

    expect(
      await screen.findByText('Concluída', {}, { timeout: 6000 })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Cancelar agendamento' })
    ).not.toBeInTheDocument();
  });

  it('refreshes the transfer and the accounts when it can no longer be cancelled', async () => {
    const transferId = addScheduledTransfer();
    const { user, queryClient } = renderApp({
      initialPath: `/transfers/${transferId}`,
    });
    const sourceAccountKey = queryKeys.accounts.detail(fakeAccountIds.ana);
    queryClient.setQueryData(sourceAccountKey, fakeDatabase.accounts[0]);

    await user.click(await findCancelButton());
    setFakeTransferStatus(transferId, 'Processing');
    await user.click(
      await screen.findByRole('button', { name: 'Confirmar cancelamento' })
    );

    expect(
      await findToast('Apenas transferências agendadas podem ser canceladas.')
    ).toBeInTheDocument();
    expect(await screen.findByText('Em processamento')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Cancelar agendamento' })
    ).not.toBeInTheDocument();
    expect(fakeDatabase.transfers[0]?.status).toBe('Processing');
    expect(queryClient.getQueryState(sourceAccountKey)?.isInvalidated).toBe(
      true
    );
  });

  it('keeps the transfer when the user goes back', async () => {
    const transferId = addScheduledTransfer();
    const { user } = renderApp({ initialPath: `/transfers/${transferId}` });

    await user.click(await findCancelButton());
    await user.click(await screen.findByRole('button', { name: 'Voltar' }));

    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    );
    expect(screen.getAllByText('Agendada')).toHaveLength(2);
    expect(
      screen.getByRole('button', { name: 'Cancelar agendamento' })
    ).toBeInTheDocument();
    expect(fakeDatabase.transfers[0]?.status).toBe('Scheduled');
  });

  it('does not offer cancellation for completed transfers', async () => {
    const transfer = addFakeTransfer({
      sourceAccountId: fakeAccountIds.ana,
      destinationAccountId: fakeAccountIds.carla,
      amount: 100,
      type: 'Immediate',
      status: 'Completed',
    });
    renderApp({ initialPath: `/transfers/${transfer.id}` });

    expect(await screen.findByText('Concluída')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Cancelar agendamento' })
    ).not.toBeInTheDocument();
  });

  it('polls while processing and shows the final status', async () => {
    const transfer = addFakeTransfer({
      sourceAccountId: fakeAccountIds.ana,
      destinationAccountId: fakeAccountIds.carla,
      amount: 100,
      type: 'Scheduled',
      status: 'Processing',
      scheduledFor: new Date(Date.now() - 60_000).toISOString(),
    });
    renderApp({ initialPath: `/transfers/${transfer.id}` });

    expect(
      await screen.findByText('Atualizando status...')
    ).toBeInTheDocument();

    setFakeTransferStatus(transfer.id, 'Completed');

    expect(
      await screen.findByText('Concluída', {}, { timeout: 6000 })
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(
        screen.queryByText('Atualizando status...')
      ).not.toBeInTheDocument()
    );
  });

  it('keeps the transfer on screen when a refresh fails', async () => {
    const transfer = addFakeTransfer({
      sourceAccountId: fakeAccountIds.ana,
      destinationAccountId: fakeAccountIds.carla,
      amount: 100,
      type: 'Scheduled',
      status: 'Processing',
      scheduledFor: new Date(Date.now() - 60_000).toISOString(),
    });
    const { queryClient } = renderApp({
      initialPath: `/transfers/${transfer.id}`,
    });

    expect(
      await screen.findByText('Atualizando status...')
    ).toBeInTheDocument();

    server.use(
      http.get(
        `${apiUrl}/transfers/:transferId`,
        () => problemResponse(500, 'UnexpectedError'),
        { once: true }
      )
    );

    await waitFor(
      () =>
        expect(
          queryClient.getQueryState(queryKeys.transfers.detail(transfer.id))
            ?.status
        ).toBe('error'),
      { timeout: 6000 }
    );
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByText(currencyText(100))).toBeInTheDocument();

    setFakeTransferStatus(transfer.id, 'Completed');

    expect(
      await screen.findByText('Concluída', {}, { timeout: 6000 })
    ).toBeInTheDocument();
  });

  it('shows the failure reason in Portuguese', async () => {
    const transfer = addFakeTransfer({
      sourceAccountId: fakeAccountIds.ana,
      destinationAccountId: fakeAccountIds.carla,
      amount: 5000,
      type: 'Immediate',
      status: 'Failed',
      failureCode: 'InsufficientFunds',
    });
    renderApp({ initialPath: `/transfers/${transfer.id}` });

    expect(await screen.findByText('Motivo da falha')).toBeInTheDocument();
    expect(
      screen.getByText('Saldo e cheque especial insuficientes.')
    ).toBeInTheDocument();
  });

  it('explains a scheduled transfer that would exceed the destination balance limit', async () => {
    const transfer = addFakeTransfer({
      sourceAccountId: fakeAccountIds.carla,
      destinationAccountId: fakeAccountIds.ana,
      amount: 100,
      type: 'Scheduled',
      status: 'Failed',
      scheduledFor: new Date(Date.now() - 60_000).toISOString(),
      failureCode: 'DestinationBalanceLimitExceeded',
    });
    renderApp({ initialPath: `/transfers/${transfer.id}` });

    expect(await screen.findByText('Motivo da falha')).toBeInTheDocument();
    expect(
      screen.getByText(
        'O saldo da conta de destino ultrapassaria o máximo permitido.'
      )
    ).toBeInTheDocument();
  });

  it('shows a not found message for unknown transfers', async () => {
    renderApp({ initialPath: `/transfers/${crypto.randomUUID()}` });

    expect(
      await screen.findByText('Transferência não encontrada')
    ).toBeInTheDocument();
    expect(
      screen.getByText('Transferência não encontrada.')
    ).toBeInTheDocument();
  });
});
