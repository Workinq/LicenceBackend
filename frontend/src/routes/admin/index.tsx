import { createFileRoute } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Metric } from '@/components/dashboard/Metric';
import { ActivityFeed } from '@/components/dashboard/ActivityFeed';
import { TopProducts } from '@/components/dashboard/TopProducts';
import { fetchLicences } from '@/api/licences';

export const Route = createFileRoute('/admin/')({
  component: OverviewPage,
});

function OverviewPage() {
  const active = useQuery({
    queryKey: ['admin-overview', 'active'],
    queryFn: () => fetchLicences({ status: 'active', limit: 1, offset: 0 }),
    staleTime: 30_000,
  });
  const total = useQuery({
    queryKey: ['admin-overview', 'total'],
    queryFn: () => fetchLicences({ limit: 1, offset: 0 }),
    staleTime: 30_000,
  });
  const revoked = useQuery({
    queryKey: ['admin-overview', 'revoked'],
    queryFn: () => fetchLicences({ status: 'revoked', limit: 1, offset: 0 }),
    staleTime: 30_000,
  });

  const activeCount = active.data?.total ?? 0;
  const totalCount = total.data?.total ?? 0;
  const revokedCount = revoked.data?.total ?? 0;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-[22px] font-semibold tracking-tight text-foreground">Overview</h1>
        <p className="text-[12.5px] text-ink-muted">Workspace health, activity, and licence trends.</p>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Metric label="Active licences" value={activeCount.toLocaleString()} />
        <Metric label="Revoked licences" value={revokedCount.toLocaleString()} />
        <Metric label="Total licences" value={totalCount.toLocaleString()} />
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-5">
        <div className="lg:col-span-3">
          <ActivityFeed limit={7} />
        </div>
        <div className="lg:col-span-2">
          <TopProducts limit={5} />
        </div>
      </div>
    </div>
  );
}
