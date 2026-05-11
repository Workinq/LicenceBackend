import { createFileRoute } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusPill } from '@/components/StatusPill';
import { LicenceKey } from '@/components/LicenceKey';
import { fetchLicence } from '@/api/licences';
import { LicenceActions } from '@/components/licences/LicenceActions';

export const Route = createFileRoute('/_authed/licences_/$id')({
  component: LicenceDetailPage,
});

function formatDateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString() : 'Never';
}

function LicenceDetailPage() {
  const { id } = Route.useParams();
  const query = useQuery({ queryKey: ['licences', 'detail', id], queryFn: () => fetchLicence(id) });

  if (query.isPending) return <Skeleton className="h-64 w-full max-w-3xl" />;
  if (query.isError || !query.data) {
    return <p className="text-sm text-status-revoked-fg">Failed to load this licence.</p>;
  }
  const lic = query.data;

  return (
    <div className="max-w-3xl space-y-6">
      <div className="flex items-center gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Licence</h1>
        <StatusPill status={lic.status} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-[10rem_1fr] gap-y-3 text-sm">
            <dt className="text-ink-muted">ID</dt>
            <dd>
              <LicenceKey value={lic.id} />
            </dd>
            <dt className="text-ink-muted">Product</dt>
            <dd className="text-ink">{lic.productSlug}</dd>
            <dt className="text-ink-muted">User</dt>
            <dd className="text-ink">{lic.userEmail}</dd>
            <dt className="text-ink-muted">HWID</dt>
            <dd className="text-ink">{lic.hwidBound ? 'Bound' : 'Not bound'}</dd>
            <dt className="text-ink-muted">IP allowlist</dt>
            <dd className="text-ink">{lic.ipAllowlist && lic.ipAllowlist.length > 0 ? lic.ipAllowlist.join(', ') : 'None'}</dd>
            <dt className="text-ink-muted">Expires</dt>
            <dd className="text-ink">{formatDateTime(lic.expiresAt)}</dd>
            <dt className="text-ink-muted">Created</dt>
            <dd className="text-ink">{formatDateTime(lic.createdAt)}</dd>
            <dt className="text-ink-muted">Notes</dt>
            <dd className="whitespace-pre-wrap text-ink">{lic.notes ?? 'None'}</dd>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Actions</CardTitle>
        </CardHeader>
        <CardContent>
          <LicenceActions licence={lic} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Bindings</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-ink-subtle">Coming in Chunk P1c-3.</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>History</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-ink-subtle">Coming in Chunk P1c-4.</p>
        </CardContent>
      </Card>
    </div>
  );
}
