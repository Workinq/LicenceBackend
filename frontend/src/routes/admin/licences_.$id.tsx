import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusPill } from '@/components/StatusPill';
import { fetchLicence, fetchLicenceSeats, fetchLicenceVerificationAttempts } from '@/api/licences';
import { LicenceActions } from '@/components/licences/LicenceActions';
import { LicenceBindings } from '@/components/licences/LicenceBindings';
import { LicenceHistory } from '@/components/licences/LicenceHistory';
import { LicenceKeys } from '@/components/licences/LicenceKeys';
import { LicenceMembers } from '@/components/licences/LicenceMembers';
import { LicenceSeats } from '@/components/licences/LicenceSeats';
import { VerificationsChart } from '@/components/licences/VerificationsChart';
import { ActivityFeed } from '@/components/dashboard/ActivityFeed';
import { KeyChip } from '@/components/dashboard/KeyChip';
import { formatRelative } from '@/lib/format';

export const Route = createFileRoute('/admin/licences_/$id')({
  component: LicenceDetailPage,
});

function formatDateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString() : 'Never';
}

function LicenceDetailPage() {
  const { id } = Route.useParams();
  const query = useQuery({ queryKey: ['licences', 'detail', id], queryFn: () => fetchLicence(id) });
  const seats = useQuery({
    queryKey: ['licence-seats', id],
    queryFn: () => fetchLicenceSeats(id),
    staleTime: 30_000,
  });
  const verifications = useQuery({
    queryKey: ['licence-verifications-stats', id],
    queryFn: () => fetchLicenceVerificationAttempts(id, { limit: 500, offset: 0 }),
    staleTime: 30_000,
  });

  if (query.isPending) return <Skeleton className="h-64 w-full" />;
  if (query.isError || !query.data) {
    return <p className="text-[12.5px] text-status-revoked-fg">Failed to load this licence.</p>;
  }
  const lic = query.data;

  const verifications7dCount = verifications.data
    ? verifications.data.items.filter((a) => Date.now() - new Date(a.attemptedAt).getTime() < 7 * 86_400_000).length
    : undefined;
  const lastVerifiedAt = verifications.data?.items[0]?.attemptedAt;
  const renewsInDays = lic.expiresAt
    ? Math.max(0, Math.floor((new Date(lic.expiresAt).getTime() - Date.now()) / 86_400_000))
    : null;

  let ipAllowlistNode: React.ReactNode;
  if (lic.ipAllowlist == null) {
    ipAllowlistNode = <span className="text-ink-subtle">None</span>;
  } else if (lic.ipAllowlist.length === 0) {
    ipAllowlistNode = <span className="text-ink-muted">Armed (binds first verifying IP)</span>;
  } else {
    ipAllowlistNode = (
      <div className="flex flex-wrap gap-1.5">
        {lic.ipAllowlist.map((cidr) => (
          <span
            key={cidr}
            className="rounded-[3px] border border-border bg-surface-sunken px-1.5 font-mono text-[11px] text-foreground"
          >
            {cidr}
          </span>
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-[20px] font-semibold tracking-tight text-foreground">{lic.productSlug}</h1>
          <KeyChip value={lic.id} display={lic.id.slice(0, 20)} />
          <StatusPill status={lic.status} />
          <div className="ml-auto">
            <LicenceActions licence={lic} />
          </div>
        </div>
        <p className="text-[12px] text-ink-muted">
          {lic.orderId ? (
            <>
              Issued from{' '}
              <Link
                to="/admin/orders/$id"
                params={{ id: lic.orderId }}
                className="font-mono text-[11.5px] text-accent hover:underline"
              >
                {lic.orderId.slice(0, 14)}
              </Link>{' '}
              · Customer{' '}
            </>
          ) : (
            <>Customer </>
          )}
          <span className="text-foreground">{lic.userEmail}</span>
        </p>
      </header>

      <div className="grid grid-cols-2 gap-px overflow-hidden rounded-md border border-border bg-border text-[12.5px] sm:grid-cols-3 lg:grid-cols-5">
        <StatCell label="Verifications (7d)" value={verifications7dCount === undefined ? '-' : String(verifications7dCount)} />
        <StatCell label="Last verified" value={lastVerifiedAt ? formatRelative(lastVerifiedAt) : 'Never'} />
        <StatCell
          label="Seats"
          value={seats.data ? `${seats.data.live.length} / ${seats.data.maxSeats}` : '-'}
        />
        <StatCell label="HWID" value={lic.hwidBound ? 'Bound' : 'Not bound'} />
        <StatCell label="Renews in" value={renewsInDays === null ? '-' : `${renewsInDays}d`} />
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <DetailCard title="Details">
          <dl className="grid grid-cols-[120px_1fr] gap-y-2.5 text-[12.5px]">
            <dt className="text-ink-muted">ID</dt>
            <dd>
              <KeyChip value={lic.id} />
            </dd>
            <dt className="text-ink-muted">Product</dt>
            <dd>
              <Link
                to="/admin/products/$id"
                params={{ id: lic.productId }}
                className="font-mono text-[12px] text-foreground hover:underline"
              >
                {lic.productSlug}
              </Link>
            </dd>
            <dt className="text-ink-muted">Customer</dt>
            <dd>{lic.userEmail}</dd>
            <dt className="text-ink-muted">IP allowlist</dt>
            <dd>{ipAllowlistNode}</dd>
            <dt className="text-ink-muted">Expires</dt>
            <dd className="font-mono text-[11.5px] text-ink-muted">{formatDateTime(lic.expiresAt)}</dd>
            <dt className="text-ink-muted">Created</dt>
            <dd className="font-mono text-[11.5px] text-ink-muted">{formatDateTime(lic.createdAt)}</dd>
            {lic.notes && (
              <>
                <dt className="text-ink-muted">Notes</dt>
                <dd className="whitespace-pre-wrap">{lic.notes}</dd>
              </>
            )}
          </dl>
        </DetailCard>

        <DetailCard title="Bindings">
          <LicenceBindings licence={lic} />
        </DetailCard>
      </div>

      <VerificationsChart licenceId={lic.id} />

      <DetailCard title="Licence keys">
        <LicenceKeys licenceId={lic.id} canMutate={true} />
      </DetailCard>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <DetailCard title="Seats">
          <LicenceSeats licenceId={lic.id} />
        </DetailCard>
        <DetailCard title="Members">
          <LicenceMembers licenceId={lic.id} />
        </DetailCard>
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <DetailCard title="History">
          <LicenceHistory licenceId={lic.id} />
        </DetailCard>
        <ActivityFeed subjectType="licence" subjectId={lic.id} limit={7} />
      </div>
    </div>
  );
}

function StatCell({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <div className="bg-card px-3 py-3">
      <div className="text-[10.5px] font-medium uppercase tracking-wide text-ink-muted">{label}</div>
      <div className="mt-1 text-[16px] font-semibold tabular-nums text-foreground">{value}</div>
    </div>
  );
}

function DetailCard({ title, children }: Readonly<{ title: string; children: React.ReactNode }>) {
  return (
    <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
      <div className="border-b border-border px-4 py-2.5">
        <h2 className="text-[13px] font-semibold text-foreground">{title}</h2>
      </div>
      <div className="p-4">{children}</div>
    </div>
  );
}
