import type { JSX } from 'react';
import { Link } from '@tanstack/react-router';

import { Button } from '@src/shared/components/ui/Button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@src/shared/components/ui/card';

export function NotFound(): JSX.Element {
  return (
    <div className='flex min-h-[60vh] items-center justify-center'>
      <Card className='w-full max-w-md text-center'>
        <CardHeader>
          <CardTitle>Página não encontrada</CardTitle>
          <CardDescription>O endereço acessado não existe.</CardDescription>
        </CardHeader>
        <CardContent />
        <CardFooter className='justify-center'>
          <Button asChild>
            <Link to='/accounts'>Voltar para contas</Link>
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
