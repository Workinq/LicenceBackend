import { cn } from '@/lib/utils';

const COLOR: Record<string, string> = {
  active: '#16a34a',
  suspended: '#d97706',
  revoked: '#dc2626',
};

export function StatusPill({ status, className }: { status: string; className?: string }) {
  const color = COLOR[status] ?? '#71717a';
  return (
    <span className={cn('inline-flex items-center gap-2', className)}>
      <span
        aria-hidden
        className="size-1.5 rounded-full"
        style={{
          background: color,
          boxShadow: `0 0 0 2px color-mix(in oklab, ${color} 20%, transparent)`,
        }}
      />
      <span className="text-xs font-medium capitalize text-foreground">{status}</span>
    </span>
  );
}
