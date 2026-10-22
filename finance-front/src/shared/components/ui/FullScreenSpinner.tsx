import { Spinner } from '@src/shared/components/ui/Spinner';
import { cn } from '@src/shared/lib/utils';

interface IFullScreenSpinnerProps {
  className?: string;
}

export function FullScreenSpinner({ className }: IFullScreenSpinnerProps) {
  return (
    <div
      className={cn(
        'absolute inset-0 z-50 flex items-center justify-center bg-background/80 backdrop-blur-sm',
        className
      )}
    >
      <Spinner className='text-primary size-1/4' />
    </div>
  );
}
