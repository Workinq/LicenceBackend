import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Compass, Download, History, ScanLine } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { fetchMyLicences } from '@/api/me-licences';
import { Metric } from '@/components/dashboard/Metric';
import { Sparkline } from '@/components/dashboard/Sparkline';
import { StatusPill } from '@/components/StatusPill';
import { formatRelative } from '@/lib/format';

export const Route = createFileRoute('/portal/')({
  component: PortalOverview,
});

const trend = (seed: number): number[] => {
  const out: number[] = [];
  let v = seed;
  for (let i = 0; i < 18; i++) {
    v += Math.sin(i * 0.5 + seed) * 4 + (Math.random() - 0.4) * 3;
    out.push(Math.max(0, v));
  }
  return out;
};

function PortalOverview() {
  const user = useAccessTokenStore((s) => s.user);
  const licences = useQuery({
    queryKey: ['portal-overview-licences'],
    queryFn: () => fetchMyLicences({ limit: 50, offset: 0 }),
  });

  const total = licences.data?.total ?? 0;
  const items = licences.data?.items ?? [];
  const boundDevices = items.filter((l) => l.hwidBound).length;
  const nextRenewalDays = items
    .map((l) => (l.expiresAt ? Math.floor((new Date(l.expiresAt).getTime() - Date.now()) / 86_400_000) : null))
    .filter((d): d is number => d !== null && d > 0)
    .sort((a, b) => a - b)[0];

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-[22px] font-semibold tracking-tight text-foreground">
          Welcome{user?.displayName ? `, ${user.displayName}` : ''}
        </h1>
        <p className="text-[12.5px] text-ink-muted">Your licences, devices, and renewals at a glance.</p>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Metric label="Active licences" value={String(total)} delta={0}>
          <Sparkline data={trend(total + 6)} />
        </Metric>
        <Metric label="Devices bound" value={String(boundDevices)} delta={0}>
          <Sparkline data={trend(boundDevices + 4)} />
        </Metric>
        <Metric label="Verifications (7d)" value="-" delta={0}>
          <Sparkline data={trend(12)} />
        </Metric>
        <Metric label="Next renewal" value={nextRenewalDays !== undefined ? `${nextRenewalDays}d` : '-'} />
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-5">
        <div className="lg:col-span-3 overflow-hidden rounded-md border border-border bg-card shadow-card">
          <div className="flex items-center justify-between border-b border-border px-4 py-2.5">
            <h2 className="text-[13px] font-semibold text-foreground">Your licences</h2>
            <Link to="/portal/licences" className="text-[11.5px] font-medium text-accent hover:underline">
              View all
            </Link>
          </div>
          <ul className="divide-y divide-border">
            {licences.isPending && (
              <li className="p-4">
                <Skeleton className="h-5 w-full" />
              </li>
            )}
            {!licences.isPending && items.length === 0 && (
              <li className="p-4 text-[12.5px] text-ink-muted">You have no licences yet.</li>
            )}
            {items.slice(0, 5).map((lic) => (
              <li key={lic.id} className="flex items-center gap-3 px-4 py-2.5 text-[12.5px]">
                <Link
                  to="/portal/licences/$id"
                  params={{ id: lic.id }}
                  className="flex-1 min-w-0 truncate font-mono text-[11.5px] text-foreground hover:underline"
                >
                  {lic.productSlug}
                </Link>
                <StatusPill status={lic.status} />
                <span className="font-mono text-[11px] text-ink-subtle">
                  {lic.expiresAt ? formatRelative(lic.expiresAt) : '-'}
                </span>
              </li>
            ))}
          </ul>
        </div>

        <div className="lg:col-span-2 overflow-hidden rounded-md border border-border bg-card shadow-card">
          <div className="border-b border-border px-4 py-2.5">
            <h2 className="text-[13px] font-semibold text-foreground">Quick actions</h2>
          </div>
          <ul className="divide-y divide-border">
            <QuickAction to="/portal/products" Icon={Compass} title="Browse catalogue" sub="Find new products" />
            <QuickAction to="/portal/licences" Icon={ScanLine} title="Bind a new device" sub="Pair this machine to a licence" />
            <QuickAction to="/portal/products" Icon={Download} title="Download CLI" sub="Latest release" />
            <QuickAction to="/portal/orders" Icon={History} title="Order history" sub="View past purchases" />
          </ul>
        </div>
      </div>
    </div>
  );
}

interface QuickActionProps {
  to: string;
  Icon: typeof Compass;
  title: string;
  sub: string;
}

function QuickAction({ to, Icon, title, sub }: QuickActionProps) {
  return (
    <li>
      <Link
        to={to}
        className="flex items-center gap-3 px-4 py-2.5 text-[12.5px] transition-colors hover:bg-surface-sunken"
      >
        <Icon className="size-4 text-ink-muted" aria-hidden />
        <div className="flex-1">
          <div className="font-medium text-foreground">{title}</div>
          <div className="text-[11px] text-ink-muted">{sub}</div>
        </div>
        <span className="text-ink-subtle">{'>'}</span>
      </Link>
    </li>
  );
}
