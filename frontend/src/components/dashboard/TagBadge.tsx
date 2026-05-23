import { cn } from '@/lib/utils';

interface TagBadgeProps {
  children: React.ReactNode;
  className?: string;
}

export function TagBadge({ children, className }: TagBadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-[3px] border border-border bg-surface-sunken px-1.5 py-0 font-mono text-[10.5px] uppercase tracking-wide leading-[1.5] text-ink-muted',
        className,
      )}
    >
      {children}
    </span>
  );
}
