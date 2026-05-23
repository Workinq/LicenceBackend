import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { fetchLicenceVerificationAttempts } from '@/api/licences';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

type Range = '24h' | '7d' | '30d' | '90d';

const RANGES: { value: Range; label: string; days: number; buckets: number }[] = [
  { value: '24h', label: '24h', days: 1, buckets: 24 },
  { value: '7d', label: '7d', days: 7, buckets: 7 },
  { value: '30d', label: '30d', days: 30, buckets: 30 },
  { value: '90d', label: '90d', days: 90, buckets: 30 },
];

export function VerificationsChart({ licenceId }: { licenceId: string }) {
  const [range, setRange] = useState<Range>('30d');
  const config = RANGES.find((r) => r.value === range)!;

  const query = useQuery({
    queryKey: ['licence-verifications', licenceId, range],
    queryFn: () => fetchLicenceVerificationAttempts(licenceId, { limit: 500, offset: 0 }),
    staleTime: 30_000,
  });

  const buckets = useMemo(() => {
    const out = Array.from({ length: config.buckets }, () => 0);
    if (!query.data) return out;
    const now = Date.now();
    const windowMs = config.days * 86_400_000;
    const bucketMs = windowMs / config.buckets;
    for (const a of query.data.items) {
      const t = new Date(a.attemptedAt).getTime();
      const age = now - t;
      if (age < 0 || age > windowMs) continue;
      const idx = config.buckets - 1 - Math.floor(age / bucketMs);
      if (idx >= 0 && idx < out.length) out[idx] += 1;
    }
    return out;
  }, [query.data, config]);

  const max = Math.max(1, ...buckets);
  const recencyCutoff = Math.max(0, config.buckets - 6);

  return (
    <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
      <div className="flex items-center justify-between border-b border-border px-4 py-2.5">
        <div>
          <h2 className="text-[13px] font-semibold text-foreground">Verifications</h2>
          <p className="text-[11.5px] text-ink-muted">Last {config.days} {config.days === 1 ? 'day' : 'days'}</p>
        </div>
        <div className="flex items-center gap-0.5 rounded-[4px] border border-border p-0.5">
          {RANGES.map((r) => (
            <button
              key={r.value}
              type="button"
              onClick={() => setRange(r.value)}
              className={cn(
                'rounded-sm px-2 py-0.5 font-mono text-[11px] font-medium transition-colors',
                range === r.value
                  ? 'bg-foreground text-background'
                  : 'text-ink-muted hover:text-foreground',
              )}
            >
              {r.label}
            </button>
          ))}
        </div>
      </div>
      <div className="p-4">
        {query.isPending ? (
          <Skeleton className="h-24 w-full" />
        ) : (
          <div className="flex h-24 items-end gap-[3px]">
            {buckets.map((count, i) => {
              const h = (count / max) * 100;
              const recent = i >= recencyCutoff;
              return (
                <div
                  key={i}
                  className="flex-1 rounded-sm"
                  style={{
                    height: `${Math.max(2, h)}%`,
                    background: 'var(--accent)',
                    opacity: recent ? 1 : 0.35,
                  }}
                  aria-label={`${count} verifications`}
                />
              );
            })}
          </div>
        )}
        <div className="mt-2 flex items-center justify-between text-[11px] text-ink-subtle">
          <span className="font-mono">{config.days}d ago</span>
          <span className="font-mono">now</span>
        </div>
      </div>
    </div>
  );
}
