import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusPill } from '@/components/StatusPill';
import { LicenceKey } from '@/components/LicenceKey';
import { fetchLicence } from '@/api/licences';
import { LicenceActions } from '@/components/licences/LicenceActions';
import { LicenceBindings } from '@/components/licences/LicenceBindings';
import { LicenceHistory } from '@/components/licences/LicenceHistory';
import { LicenceMembers } from '@/components/licences/LicenceMembers';
import { LicenceSeats } from '@/components/licences/LicenceSeats';

export const Route = createFileRoute('/admin/licences_/$id')({
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
          <dl className="grid grid-cols-[10rem_1fr] items-baseline gap-y-3 text-sm">
            <dt className="text-ink-muted">ID</dt>
            <dd>
              <LicenceKey value={lic.id} />
            </dd>
            <dt className="text-ink-muted">Licence key</dt>
            <dd className="text-ink-subtle">Shown only when the licence is created or regenerated.</dd>
            <dt className="text-ink-muted">Product</dt>
            <dd>
              <Link
                to="/admin/products/$id"
                params={{ id: lic.productId }}
                className="text-ink underline-offset-2 hover:underline"
              >
                {lic.productSlug}
              </Link>
            </dd>
            <dt className="text-ink-muted">User</dt>
            <dd className="text-ink">{lic.userEmail}</dd>
            <dt className="text-ink-muted">HWID</dt>
            <dd className="text-ink">{lic.hwidBound ? 'Bound' : 'Not bound'}</dd>
            <dt className="text-ink-muted">IP allowlist</dt>
            <dd className="text-ink">{lic.ipAllowlist == null ? 'None' : lic.ipAllowlist.length === 0 ? 'Armed (binds the first verifying IP)' : lic.ipAllowlist.join(', ')}</dd>
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
          <LicenceBindings licence={lic} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Seats</CardTitle>
        </CardHeader>
        <CardContent>
          <LicenceSeats licenceId={lic.id} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Members</CardTitle>
        </CardHeader>
        <CardContent>
          <LicenceMembers licenceId={lic.id} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>History</CardTitle>
        </CardHeader>
        <CardContent>
          <LicenceHistory licenceId={lic.id} />
        </CardContent>
      </Card>
    </div>
  );
}
