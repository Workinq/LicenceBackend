import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { Skeleton } from '@/components/ui/skeleton';

export interface AuditEvent {
  id: string;
  icon: LucideIcon;
  title: ReactNode;
  meta?: ReactNode;
  timestamp: string;
}

interface AuditTimelineProps {
  events: AuditEvent[];
  isLoading: boolean;
  isError: boolean;
  errorText?: string;
  emptyText?: string;
}

function formatWhen(value: string): string {
  return new Date(value).toLocaleString();
}

export function AuditTimeline({
  events,
  isLoading,
  isError,
  errorText = 'Could not load this history.',
  emptyText = 'Nothing here yet.',
}: Readonly<AuditTimelineProps>) {
  if (isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-12 w-full" />
      </div>
    );
  }
  if (isError) return <p className="text-sm text-status-revoked-fg">{errorText}</p>;
  if (events.length === 0) return <p className="text-sm text-ink-muted">{emptyText}</p>;

  return (
    <ol className="space-y-4">
      {events.map((e) => {
        const Icon = e.icon;
        return (
          <li key={e.id} className="flex gap-3">
            <span className="mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-full bg-surface-sunken text-ink-muted">
              <Icon className="size-3.5" aria-hidden="true" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="text-sm text-ink">{e.title}</p>
              {e.meta && <p className="text-xs text-ink-muted">{e.meta}</p>}
              <p className="text-xs text-ink-subtle">{formatWhen(e.timestamp)}</p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
