import { Link } from '@tanstack/react-router';
import { Button } from '@/components/ui/button';

export function NotFound() {
  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 text-center">
      <p className="font-display text-3xl font-semibold text-ink">Page not found</p>
      <p className="text-sm text-ink-muted">That page does not exist or has moved.</p>
      <Button asChild>
        <Link to="/">Go to overview</Link>
      </Button>
    </div>
  );
}
