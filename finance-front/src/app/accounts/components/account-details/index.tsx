import type { JSX } from 'react';
import { getRouteApi, Link } from '@tanstack/react-router';

import { TransferHistory } from '@src/app/transfers/components/TransferHistory';
import { TransferLimitsCard } from '@src/app/transfers/components/TransferLimitsCard';
import { ApiErrorAlert } from '@src/shared/components/ApiErrorAlert';
import { PageHeader } from '@src/shared/components/PageHeader';
import { Button } from '@src/shared/components/ui/Button';
import { FullScreenSpinner } from '@src/shared/components/ui/FullScreenSpinner';
import {
  formatDocumentNumber,
  getApiErrorMessage,
  isNotFoundProblem,
} from '@src/shared/utils';

import { useAccount } from '../../hooks/useAccount';
import { AccountEntries } from './AccountEntries';
import { BalanceCards } from './BalanceCards';
import { EditOverdraftLimitModal } from './EditOverdraftLimitModal';

const accountDetailsRoute = getRouteApi('/accounts/$accountId');

function findLoadErrorTitle(error: unknown): string {
  return isNotFoundProblem(error)
    ? 'Conta não encontrada'
    : 'Não foi possível carregar a conta';
}

export function AccountDetails(): JSX.Element {
  const { accountId } = accountDetailsRoute.useParams();
  const { account, isPending, isError, error } = useAccount(accountId);

  return (
    <div className='flex flex-col gap-6'>
      <PageHeader
        title='Detalhes da conta'
        description={
          account &&
          `${account.holderName} · CPF ${formatDocumentNumber(account.documentNumber)}`
        }
        actions={
          account && (
            <>
              <Button asChild size='sm'>
                <Link
                  to='/transfers'
                  search={{ tab: 'immediate', sourceAccountId: accountId }}
                >
                  Nova transferência
                </Link>
              </Button>
              <EditOverdraftLimitModal account={account} />
            </>
          )
        }
      />

      {isPending && <FullScreenSpinner />}

      {isError && !account && (
        <ApiErrorAlert
          title={findLoadErrorTitle(error)}
          message={getApiErrorMessage(error)}
        />
      )}

      {account && (
        <>
          <BalanceCards account={account} />
          <TransferLimitsCard accountId={accountId} />
          <AccountEntries accountId={accountId} />
          <TransferHistory accountId={accountId} />
        </>
      )}
    </div>
  );
}
