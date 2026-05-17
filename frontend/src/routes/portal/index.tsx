import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { fetchMyLicences } from '@/api/me-licences';
import { fetchProducts } from '@/api/products';

export const Route = createFileRoute('/portal/')({
  component: PortalOverview,
});

function PortalOverview() {
  const user = useAccessTokenStore((s) => s.user);
  const licences = useQuery({
    queryKey: ['portal', 'overview', 'licences'],
    queryFn: () => fetchMyLicences({ limit: 1, offset: 0 }),
  });
  const products = useQuery({
    queryKey: ['portal', 'overview', 'products'],
    queryFn: () => fetchProducts({ limit: 1, offset: 0 }),
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
        <SummaryCard
          title="Your licences"
          description="Licences you own or are a member of."
          isPending={licences.isPending}
          isError={licences.isError}
          count={licences.data?.total}
          to="/portal/licences"
          linkLabel="View all"
        />
        <SummaryCard
          title="Browse products"
          description="Products available in the catalog."
          isPending={products.isPending}
          isError={products.isError}
          count={products.data?.total}
          to="/portal/products"
          linkLabel="Open catalog"
        />
      </div>
    </div>
  );
}

interface SummaryCardProps {
  title: string;
  description: string;
  isPending: boolean;
  isError: boolean;
  count: number | undefined;
  to: '/portal/licences' | '/portal/products';
  linkLabel: string;
}

function SummaryCard({ title, description, isPending, isError, count, to, linkLabel }: SummaryCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {isPending && <Skeleton className="h-9 w-24" />}
        {isError && <p className="text-sm text-status-revoked-fg">Failed to load.</p>}
        {!isPending && !isError && (
          <p className="font-display text-3xl font-semibold text-ink">{count ?? 0}</p>
        )}
        <p className="text-sm text-ink-muted">{description}</p>
        <Link to={to} className="text-sm font-medium text-ink underline-offset-2 hover:underline">
          {linkLabel}
        </Link>
      </CardContent>
    </Card>
  );
}
