import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { fetchAuditEvents } from '@/api/audit-events';
import { TagBadge } from './TagBadge';
import { formatRelative } from '@/lib/format';
import { Skeleton } from '@/components/ui/skeleton';

interface ActivityFeedProps {
  limit?: number;
  subjectType?: string;
  subjectId?: string;
}

const TAG_LABELS: Record<string, string> = {
  'licence.created': 'issue',
  'licence.status_changed': 'status',
  'licence.suspended': 'suspend',
  'licence.revoked': 'revoke',
  'licence.regenerated': 'regen',
  'licence.binding_changed': 'bind',
  'licence.member_added': 'member',
  'licence.member_removed': 'member',
  'licence.seat_claimed': 'seat',
  'licence.label_changed': 'label',
  'licence.ip_allowlist_changed': 'config',
  'licence.max_seats_changed': 'config',
  'verification.attempt': 'verify',
  'order.completed': 'order',
  'order.refunded': 'order',
  'product.created': 'product',
  'product.updated': 'product',
  'user.created': 'user',
  'user.updated': 'user',
};

const tagFor = (eventType: string): string => TAG_LABELS[eventType] ?? eventType.split('.')[0];

export function ActivityFeed({ limit = 7, subjectType, subjectId }: ActivityFeedProps) {
  const query = useQuery({
    queryKey: ['audit-events', { subjectType, subjectId, limit }],
    queryFn: () =>
      fetchAuditEvents({
        limit,
        offset: 0,
        ...(subjectType ? { subject_type: subjectType } : {}),
        ...(subjectId ? { subject_id: subjectId } : {}),
      }),
    staleTime: 15_000,
  });

  return (
    <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
      <div className="flex items-center justify-between border-b border-border px-4 py-2.5">
        <h2 className="text-[13px] font-semibold text-foreground">Recent activity</h2>
        <Link to="/admin/licences" className="text-[11.5px] font-medium text-accent hover:underline">
          View all
        </Link>
      </div>
      <ol className="divide-y divide-border">
        {query.isPending && (
          <li className="p-4">
            <Skeleton className="h-5 w-full" />
          </li>
        )}
        {query.isError && (
          <li className="p-4 text-[12.5px] text-status-revoked-fg">Failed to load activity.</li>
        )}
        {query.data?.items.map((event) => {
          const subject = event.subjectId.slice(0, 14);
          const tag = tagFor(event.eventType);
          return (
            <li key={event.id} className="flex items-start gap-3 px-4 py-2.5">
              <TagBadge>{tag}</TagBadge>
              <div className="flex-1 text-[12.5px] leading-relaxed text-foreground">
                <span className="text-ink-muted">{event.eventType}</span>{' '}
                <span className="font-mono text-[11.5px] text-foreground">{subject}</span>
                {event.reason && <span className="text-ink-muted"> — {event.reason}</span>}
              </div>
              <span className="font-mono text-[11px] text-ink-subtle">{formatRelative(event.occurredAt)}</span>
              {event.actorUserEmail && (
                <span className="hidden font-mono text-[11px] text-ink-muted md:inline">{event.actorUserEmail}</span>
              )}
            </li>
          );
        })}
        {query.data && query.data.items.length === 0 && (
          <li className="p-4 text-[12.5px] text-ink-muted">No recent activity.</li>
        )}
      </ol>
    </div>
  );
}
