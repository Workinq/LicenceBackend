import type { ErrorComponentProps } from '@tanstack/react-router';
import { Button } from '@/components/ui/button';

export function RootErrorBoundary({ error, reset }: ErrorComponentProps) {
  const message = error instanceof Error ? error.message : 'An unexpected error occurred.';
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-surface px-4 text-center text-ink">
      <p className="font-display text-3xl font-semibold">Something went wrong</p>
      <p className="max-w-md text-sm text-ink-muted">{message}</p>
      <div className="flex gap-3">
        <Button onClick={reset}>Try again</Button>
        <Button variant="outline" onClick={() => globalThis.location.assign('/')}>
          Go to overview
        </Button>
      </div>
    </div>
  );
}
