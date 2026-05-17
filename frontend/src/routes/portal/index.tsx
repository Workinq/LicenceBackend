import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { fetchMyLicences } from '@/api/me-licences';

export const Route = createFileRoute('/portal/')({
  component: PortalOverview,
});

function PortalOverview() {
  const user = useAccessTokenStore((s) => s.user);
  const summary = useQuery({
    queryKey: ['portal', 'overview', 'licences'],
    queryFn: () => fetchMyLicences({ limit: 1, offset: 0 }),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-semibold text-ink">
          Welcome{user?.displayName ? `, ${user.displayName}` : ''}
        </h1>
        <p className="text-sm text-ink-muted">
          Manage the licences you own or have access to.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Your licences</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {summary.isPending && <Skeleton className="h-8 w-24" />}
            {summary.isError && (
              <p className="text-sm text-status-revoked-fg">Failed to load.</p>
            )}
            {summary.data && (
              <>
                <p className="font-display text-3xl font-semibold text-ink">{summary.data.total}</p>
                <Link
                  to="/portal/licences"
                  className="text-sm font-medium text-ink underline-offset-2 hover:underline"
                >
                  View all
                </Link>
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Browse products</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <p className="text-sm text-ink-muted">See what is available in the catalog.</p>
            <Link
              to="/portal/products"
              className="text-sm font-medium text-ink underline-offset-2 hover:underline"
            >
              Open catalog
            </Link>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
