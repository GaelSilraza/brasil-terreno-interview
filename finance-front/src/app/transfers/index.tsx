import type { JSX } from 'react';
import { getRouteApi } from '@tanstack/react-router';

import { PageHeader } from '@src/shared/components/PageHeader';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@src/shared/components/ui/card';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@src/shared/components/ui/tabs';

import { NewTransferForm } from './components/NewTransferForm';
import { ScheduleTransferForm } from './components/ScheduleTransferForm';
import { TransferLookupForm } from './components/TransferLookupForm';

type TransferTab = 'immediate' | 'scheduled';

const transfersRoute = getRouteApi('/transfers');

function isTransferTab(value: string): value is TransferTab {
  return value === 'immediate' || value === 'scheduled';
}

export function Transfers(): JSX.Element {
  const { tab, sourceAccountId } = transfersRoute.useSearch();
  const navigate = transfersRoute.useNavigate();
  const selectedTab = tab ?? 'immediate';

  function handleChangeTab(value: string): void {
    if (!isTransferTab(value)) {
      return;
    }

    void navigate({
      search: (previousSearch) => ({ ...previousSearch, tab: value }),
    });
  }

  return (
    <>
      <PageHeader
        title='Transferências'
        description='Envie agora ou agende para uma data futura.'
      />
      <Tabs
        value={selectedTab}
        onValueChange={handleChangeTab}
        className='gap-4'
      >
        <TabsList>
          <TabsTrigger value='immediate'>Imediata</TabsTrigger>
          <TabsTrigger value='scheduled'>Agendada</TabsTrigger>
        </TabsList>
        <div className='grid items-start gap-6 lg:grid-cols-[2fr_1fr]'>
          <div>
            <TabsContent value='immediate'>
              <Card>
                <CardHeader>
                  <CardTitle>Transferência imediata</CardTitle>
                </CardHeader>
                <CardContent>
                  <NewTransferForm initialSourceAccountId={sourceAccountId} />
                </CardContent>
              </Card>
            </TabsContent>
            <TabsContent value='scheduled'>
              <Card>
                <CardHeader>
                  <CardTitle>Transferência agendada</CardTitle>
                </CardHeader>
                <CardContent>
                  <ScheduleTransferForm
                    initialSourceAccountId={sourceAccountId}
                  />
                </CardContent>
              </Card>
            </TabsContent>
          </div>
          <Card>
            <CardHeader>
              <CardTitle>Consultar transferência</CardTitle>
            </CardHeader>
            <CardContent>
              <TransferLookupForm />
            </CardContent>
          </Card>
        </div>
      </Tabs>
    </>
  );
}
