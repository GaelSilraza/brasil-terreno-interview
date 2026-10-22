import { screen, waitFor, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';
import { http } from 'msw';
import { describe, expect, it } from 'vitest';

import { apiUrl } from '@src/test/apiUrl';
import {
  fakeAccountIds,
  fakeDatabase,
  validationProblemResponse,
} from '@src/test/fake-api';
import { currencyText } from '@src/test/currencyText';
import { renderApp } from '@src/test/renderApp';
import { server } from '@src/test/server';

async function findToast(message: string): Promise<HTMLElement> {
  const notifications = screen.getByRole('region', { name: /Notificações/ });
  return within(notifications).findByText(message);
}

async function replaceValue(
  user: UserEvent,
  label: string,
  value: string
): Promise<void> {
  const input = screen.getByLabelText(label);
  await user.clear(input);
  await user.type(input, value);
}

function findLimitsRegion(): Promise<HTMLElement> {
  return screen.findByRole('region', { name: 'Limites por hora' });
}

async function openLimitsModal(user: UserEvent): Promise<HTMLElement> {
  const limitsRegion = await findLimitsRegion();
  await user.click(
    await within(limitsRegion).findByRole('button', {
      name: 'Alterar limites',
    })
  );
  await screen.findByRole('dialog');
  return limitsRegion;
}

describe('Transfer limits on the account page', () => {
  it('shows the default hourly limits and the current usage', async () => {
    renderApp({ initialPath: `/accounts/${fakeAccountIds.ana}` });

    const limitsRegion = await findLimitsRegion();

    expect(await within(limitsRegion).findByText('Padrão')).toBeInTheDocument();
    expect(
      within(
        within(limitsRegion).getByRole('row', { name: /Diurno/ })
      ).getByText(currencyText(5000))
    ).toBeInTheDocument();
    expect(
      within(
        within(limitsRegion).getByRole('row', { name: /Noturno/ })
      ).getByText(currencyText(1000))
    ).toBeInTheDocument();
    expect(
      within(limitsRegion).getByText(/Tentativas na última hora: 0 de (5|3)/)
    ).toBeInTheDocument();
  });

  it('saves custom hourly limits', async () => {
    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.ana}`,
    });

    const limitsRegion = await openLimitsModal(user);
    await replaceValue(user, 'Valor máximo diurno (R$)', '800000');
    await replaceValue(user, 'Tentativas diurnas por hora', '10');
    await replaceValue(user, 'Valor máximo noturno (R$)', '200000');
    await replaceValue(user, 'Tentativas noturnas por hora', '4');
    await user.click(screen.getByRole('button', { name: 'Salvar limites' }));

    expect(await findToast('Limites atualizados.')).toBeInTheDocument();
    expect(
      await within(limitsRegion).findByText('Personalizado')
    ).toBeInTheDocument();
    expect(
      within(
        within(limitsRegion).getByRole('row', { name: /Diurno/ })
      ).getByText(currencyText(8000))
    ).toBeInTheDocument();
    expect(fakeDatabase.limitPolicies.get(fakeAccountIds.ana)).toEqual({
      daytimeMaxAmountPerHour: 8000,
      daytimeMaxAttemptsPerHour: 10,
      nighttimeMaxAmountPerHour: 2000,
      nighttimeMaxAttemptsPerHour: 4,
    });
  });

  it('validates the limits before sending', async () => {
    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.ana}`,
    });

    await openLimitsModal(user);
    await replaceValue(user, 'Tentativas diurnas por hora', '0');
    await user.click(screen.getByRole('button', { name: 'Salvar limites' }));

    expect(
      await screen.findByText('Informe no mínimo 1 tentativa.')
    ).toBeInTheDocument();
    expect(fakeDatabase.limitPolicies.size).toBe(0);
  });

  it('saves custom limits using the money mask', async () => {
    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.ana}`,
    });

    const limitsRegion = await openLimitsModal(user);
    const daytimeAmountInput = screen.getByLabelText(
      'Valor máximo diurno (R$)'
    );

    expect(daytimeAmountInput).toHaveValue('5.000,00');
    expect(screen.getByLabelText('Valor máximo noturno (R$)')).toHaveValue(
      '1.000,00'
    );

    await user.clear(daytimeAmountInput);
    await user.type(daytimeAmountInput, '800000');

    expect(daytimeAmountInput).toHaveValue('8.000,00');

    await replaceValue(user, 'Tentativas diurnas por hora', '8');
    await user.click(screen.getByRole('button', { name: 'Salvar limites' }));

    expect(
      await within(limitsRegion).findByText('Personalizado')
    ).toBeInTheDocument();
    expect(
      within(
        within(limitsRegion).getByRole('row', { name: /Diurno/ })
      ).getByText(currencyText(8000))
    ).toBeInTheDocument();
    expect(fakeDatabase.limitPolicies.get(fakeAccountIds.ana)).toEqual({
      daytimeMaxAmountPerHour: 8000,
      daytimeMaxAttemptsPerHour: 8,
      nighttimeMaxAmountPerHour: 1000,
      nighttimeMaxAttemptsPerHour: 3,
    });
  });

  it('shows the server field error under the attempts field', async () => {
    server.use(
      http.put(`${apiUrl}/transfer-limits/:accountId`, () =>
        validationProblemResponse([
          {
            field: 'daytimeMaxAttemptsPerHour',
            code: 'OutOfRange',
            params: { from: 1, to: 1000 },
          },
        ])
      )
    );
    const { user } = renderApp({
      initialPath: `/accounts/${fakeAccountIds.ana}`,
    });

    await openLimitsModal(user);
    await user.click(screen.getByRole('button', { name: 'Salvar limites' }));

    const attemptsInput = screen.getByLabelText('Tentativas diurnas por hora');

    await waitFor(() =>
      expect(attemptsInput).toHaveAccessibleDescription(
        'Informe um valor entre 1 e 1000.'
      )
    );
    expect(attemptsInput).toHaveAttribute('aria-invalid', 'true');
    expect(
      screen.queryByText('Não foi possível salvar os limites')
    ).not.toBeInTheDocument();
    expect(fakeDatabase.limitPolicies.size).toBe(0);
  });
});
