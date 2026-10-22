import { screen, waitFor, within } from '@testing-library/react';
import { http } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';

import { apiUrl } from '@src/test/apiUrl';
import {
  fakeDatabase,
  seedManyFakeAccounts,
  validationProblemResponse,
} from '@src/test/fake-api';
import { currencyText } from '@src/test/currencyText';
import { renderApp, type IRenderAppResult } from '@src/test/renderApp';
import { server } from '@src/test/server';

const seededHolderNames = [
  'Ana Souza',
  'Bruno Lima',
  'Carla Mendes',
  'Daniel Rocha',
  'Elisa Martins',
];

function readHolderNames(): string[] {
  const rowGroups = screen.queryAllByRole('rowgroup');
  const bodyRowGroup = rowGroups.at(1);

  if (!bodyRowGroup) {
    return [];
  }

  return within(bodyRowGroup)
    .queryAllByRole('row')
    .map((row) => within(row).getAllByRole('cell')[0]?.textContent ?? '');
}

async function openNewAccountModal({ user }: IRenderAppResult): Promise<void> {
  await screen.findByRole('row', { name: /Ana Souza/ });
  await user.click(screen.getByRole('button', { name: 'Nova conta' }));
  await screen.findByLabelText('Nome completo');
}

describe('Accounts page', () => {
  afterEach(() => {
    server.events.removeAllListeners();
  });

  it('lists the seeded accounts with balances and statuses', async () => {
    renderApp({ initialPath: '/accounts' });

    expect(
      await screen.findByRole('row', { name: /Ana Souza/ })
    ).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /Bruno Lima/ })).toBeInTheDocument();
    expect(screen.getByText(currencyText(-800))).toBeInTheDocument();
    expect(screen.getByText('100.000.000-01')).toBeInTheDocument();

    const danielRow = screen.getByRole('row', { name: /Daniel Rocha/ });
    expect(within(danielRow).getByText('Bloqueada')).toBeInTheDocument();
  });

  it('creates a new account and shows it in the list', async () => {
    const renderResult = renderApp({ initialPath: '/accounts' });
    const { user } = renderResult;

    await openNewAccountModal(renderResult);
    await user.type(screen.getByLabelText('Nome completo'), 'Fernanda Alves');
    await user.type(screen.getByLabelText('CPF'), '10000000006');
    await user.type(screen.getByLabelText('Saldo inicial (R$)'), '25000');
    await user.type(
      screen.getByLabelText('Limite de cheque especial (R$)'),
      '30000'
    );
    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    expect(
      await screen.findByText('Conta criada com sucesso.')
    ).toBeInTheDocument();
    expect(
      await screen.findByRole('row', { name: /Fernanda Alves/ })
    ).toBeInTheDocument();
    expect(
      fakeDatabase.accounts.find(
        (account) => account.holderName === 'Fernanda Alves'
      )
    ).toMatchObject({ balance: 250, overdraftLimit: 300 });
  });

  it('shows the API message when the document already exists', async () => {
    const renderResult = renderApp({ initialPath: '/accounts' });
    const { user } = renderResult;

    await openNewAccountModal(renderResult);
    await user.type(screen.getByLabelText('Nome completo'), 'Outra Ana Souza');
    await user.type(screen.getByLabelText('CPF'), '10000000001');
    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(
      'Já existe uma pessoa cadastrada com este CPF.'
    );
    expect(fakeDatabase.accounts).toHaveLength(5);
  });

  it('filters accounts by holder name', async () => {
    const { user } = renderApp({ initialPath: '/accounts' });

    await screen.findByRole('row', { name: /Ana Souza/ });
    const searchInput = screen.getByLabelText('Buscar por titular ou CPF');

    await user.type(searchInput, 'Carla{Enter}');

    await waitFor(() => expect(readHolderNames()).toEqual(['Carla Mendes']));

    await user.clear(searchInput);
    await user.type(searchInput, 'Ninguém');

    expect(
      await screen.findByText('Nenhuma conta encontrada para "Ninguém".')
    ).toBeInTheDocument();
  });

  it('opens the account details from the actions menu', async () => {
    const { user } = renderApp({ initialPath: '/accounts' });

    await user.click(
      await screen.findByRole('button', { name: 'Ações de Ana Souza' })
    );
    await user.click(
      await screen.findByRole('menuitem', { name: 'Ver detalhes' })
    );

    expect(
      await screen.findByRole('heading', { name: 'Detalhes da conta' })
    ).toBeInTheDocument();
    expect(
      await screen.findByText('Ana Souza · CPF 100.000.000-01')
    ).toBeInTheDocument();
  });

  it('paginates the accounts table with next and previous cursors', async () => {
    seedManyFakeAccounts(45);
    const { user } = renderApp({ initialPath: '/accounts' });

    await screen.findByRole('row', { name: /Ana Souza/ });
    const firstPageNames = readHolderNames();
    expect(firstPageNames).toHaveLength(20);
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Próxima' }));
    await waitFor(() =>
      expect(readHolderNames()).not.toContain(firstPageNames[0])
    );
    const secondPageNames = readHolderNames();
    expect(secondPageNames).toHaveLength(20);

    await user.click(screen.getByRole('button', { name: 'Próxima' }));
    await waitFor(() => expect(readHolderNames()).toHaveLength(10));
    const thirdPageNames = readHolderNames();
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Anterior' }));
    await waitFor(() => expect(readHolderNames()).toEqual(secondPageNames));

    const allNames = [...firstPageNames, ...secondPageNames, ...thirdPageNames];
    const expectedNames = fakeDatabase.accounts.map(
      (account) => account.holderName
    );
    expect(allNames).toHaveLength(50);
    expect(new Set(allNames).size).toBe(50);
    expect([...allNames].sort()).toEqual([...expectedNames].sort());
  });

  it('searches accounts on the server by name', async () => {
    seedManyFakeAccounts(45);
    const { user } = renderApp({ initialPath: '/accounts' });

    await screen.findByRole('row', { name: /Ana Souza/ });
    await user.type(
      screen.getByLabelText('Buscar por titular ou CPF'),
      'Cliente 0042'
    );

    await waitFor(() => expect(readHolderNames()).toEqual(['Cliente 0042']));
  });

  it('searches accounts by document number typed with mask', async () => {
    seedManyFakeAccounts(45);
    const { user } = renderApp({ initialPath: '/accounts' });

    await screen.findByRole('row', { name: /Ana Souza/ });
    await user.type(
      screen.getByLabelText('Buscar por titular ou CPF'),
      '100.000.000'
    );

    await waitFor(() => expect(readHolderNames()).toEqual(seededHolderNames));
  });

  it('shows the CPF with mask and sends only digits', async () => {
    const requestBodies: Promise<unknown>[] = [];
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'POST' && request.url === `${apiUrl}/accounts`) {
        requestBodies.push(request.clone().json());
      }
    });
    const renderResult = renderApp({ initialPath: '/accounts' });
    const { user } = renderResult;

    await openNewAccountModal(renderResult);
    await user.type(screen.getByLabelText('Nome completo'), 'Fernanda Alves');
    await user.type(screen.getByLabelText('CPF'), '12345678901');

    expect(screen.getByLabelText('CPF')).toHaveValue('123.456.789-01');

    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    expect(
      await screen.findByText('Conta criada com sucesso.')
    ).toBeInTheDocument();
    expect(fakeDatabase.accounts).toContainEqual(
      expect.objectContaining({ documentNumber: '12345678901' })
    );
    expect(requestBodies).toHaveLength(1);
    expect(await requestBodies[0]).toMatchObject({
      documentNumber: '12345678901',
    });
  });

  it('shows money fields with two decimals while typing', async () => {
    const renderResult = renderApp({ initialPath: '/accounts' });
    const { user } = renderResult;

    await openNewAccountModal(renderResult);
    const initialBalanceInput = screen.getByLabelText('Saldo inicial (R$)');

    expect(initialBalanceInput).toHaveValue('0,00');

    await user.type(initialBalanceInput, '25050');

    expect(initialBalanceInput).toHaveValue('250,50');

    await user.type(screen.getByLabelText('Nome completo'), 'Fernanda Alves');
    await user.type(screen.getByLabelText('CPF'), '10000000006');
    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    expect(
      await screen.findByText('Conta criada com sucesso.')
    ).toBeInTheDocument();
    expect(
      fakeDatabase.accounts.find(
        (account) => account.holderName === 'Fernanda Alves'
      )
    ).toMatchObject({ balance: 250.5, overdraftLimit: 0 });
  });

  it('shows the server field error under the field', async () => {
    server.use(
      http.post(`${apiUrl}/accounts`, () =>
        validationProblemResponse([
          { field: 'documentNumber', code: 'InvalidFormat' },
        ])
      )
    );
    const renderResult = renderApp({ initialPath: '/accounts' });
    const { user } = renderResult;

    await openNewAccountModal(renderResult);
    await user.type(screen.getByLabelText('Nome completo'), 'Fernanda Alves');
    await user.type(screen.getByLabelText('CPF'), '10000000006');
    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    const documentNumberInput = screen.getByLabelText('CPF');

    await waitFor(() =>
      expect(documentNumberInput).toHaveAccessibleDescription(
        'Formato inválido.'
      )
    );
    expect(documentNumberInput).toHaveAttribute('aria-invalid', 'true');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('blocks submission when basic rules fail', async () => {
    const renderResult = renderApp({ initialPath: '/accounts' });
    const { user } = renderResult;

    await openNewAccountModal(renderResult);
    await user.type(screen.getByLabelText('Nome completo'), 'Al');
    await user.type(screen.getByLabelText('CPF'), '123');
    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    await waitFor(() =>
      expect(
        screen.getByLabelText('Nome completo')
      ).toHaveAccessibleDescription('Informe o nome completo.')
    );
    expect(screen.getByLabelText('CPF')).toHaveAccessibleDescription(
      'Informe os 11 dígitos do CPF.'
    );
    expect(fakeDatabase.accounts).toHaveLength(5);
  });
});
