import { cn } from '@/lib/utils';

const PALETTE: Record<string, string> = {
  active: 'bg-status-active-bg text-status-active-fg',
  suspended: 'bg-status-suspended-bg text-status-suspended-fg',
  revoked: 'bg-status-revoked-bg text-status-revoked-fg',
};

export function StatusPill({ status, className }: { status: string; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-pill px-2.5 py-0.5 text-xs font-medium capitalize',
        PALETTE[status] ?? 'bg-surface-sunken text-ink-muted',
        className,
      )}
    >
      {status}
    </span>
  );
}
