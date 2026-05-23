import { ArrowDown, ArrowUp } from 'lucide-react';
import { cn } from '@/lib/utils';

interface MetricProps {
  label: string;
  value: string;
  delta?: number;
  deltaSuffix?: string;
  children?: React.ReactNode;
  className?: string;
}

export function Metric({ label, value, delta, deltaSuffix = '%', children, className }: MetricProps) {
  const positive = delta !== undefined && delta >= 0;
  const deltaColor = positive ? 'text-status-active-fg' : 'text-status-revoked-fg';

  return (
    <div className={cn('flex flex-col gap-2 rounded-md border border-border bg-card p-4 shadow-card', className)}>
      <span className="text-[10.5px] font-medium uppercase tracking-wide text-ink-muted">{label}</span>
      <div className="flex items-baseline gap-2">
        <span className="text-2xl font-semibold tabular-nums tracking-tight text-foreground">{value}</span>
        {delta !== undefined && (
          <span className={cn('inline-flex items-center gap-0.5 text-[11.5px] font-medium tabular-nums', deltaColor)}>
            {positive ? <ArrowUp className="size-3" /> : <ArrowDown className="size-3" />}
            {Math.abs(delta).toFixed(1)}
            {deltaSuffix}
          </span>
        )}
      </div>
      {children && <div className="-mx-1 mt-1 h-8">{children}</div>}
    </div>
  );
}
