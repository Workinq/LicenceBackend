import { createFileRoute } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Metric } from '@/components/dashboard/Metric';
import { Sparkline } from '@/components/dashboard/Sparkline';
import { ActivityFeed } from '@/components/dashboard/ActivityFeed';
import { TopProducts } from '@/components/dashboard/TopProducts';
import { fetchLicences } from '@/api/licences';

export const Route = createFileRoute('/admin/')({
  component: OverviewPage,
});

const placeholderSpark = (seed: number): number[] => {
  const out: number[] = [];
  let v = seed;
  for (let i = 0; i < 24; i++) {
    v += Math.sin(i * 0.7 + seed) * 6 + (Math.random() - 0.4) * 4;
    out.push(Math.max(0, v));
  }
  return out;
};

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
  // Placeholder until /api/admin/metrics ships.
  const verifications24h = '—';
  const mrr = '—';

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-[22px] font-semibold tracking-tight text-foreground">Overview</h1>
        <p className="text-[12.5px] text-ink-muted">Workspace health, activity, and licence trends.</p>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Metric label="Active licences" value={activeCount.toLocaleString()} delta={4.2}>
          <Sparkline data={placeholderSpark(activeCount || 24)} />
        </Metric>
        <Metric label="Verifications / 24h" value={verifications24h} delta={2.1}>
          <Sparkline data={placeholderSpark(40)} />
        </Metric>
        <Metric label="MRR" value={mrr} delta={1.4}>
          <Sparkline data={placeholderSpark(60)} />
        </Metric>
        <Metric label="Revocations / 24h" value={String(revokedCount)} delta={-0.4}>
          <Sparkline data={placeholderSpark(8)} color="var(--destructive)" />
        </Metric>
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-5">
        <div className="lg:col-span-3">
          <ActivityFeed limit={7} />
        </div>
        <div className="lg:col-span-2">
          <TopProducts limit={5} />
        </div>
      </div>

      <p className="text-[11px] text-ink-subtle">
        Total licences in workspace: <span className="font-mono tabular-nums">{totalCount.toLocaleString()}</span>
      </p>
    </div>
  );
}
