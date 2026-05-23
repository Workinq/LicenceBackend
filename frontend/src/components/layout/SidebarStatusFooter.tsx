import { useQuery } from '@tanstack/react-query';
import { fetchHealth } from '@/api/health';

export function SidebarStatusFooter() {
  const { data } = useQuery({
    queryKey: ['health'],
    queryFn: fetchHealth,
    refetchInterval: 60_000,
    staleTime: 30_000,
  });

  const ok = data?.status === 'ok';
  let color: string;
  let label: string;
  if (ok) {
    color = '#16a34a';
    label = 'API operational';
  } else if (data) {
    color = '#dc2626';
    label = 'API degraded';
  } else {
    color = '#a1a1aa';
    label = 'Checking...';
  }

  return (
    <div className="mt-auto border-t border-border px-3 py-2.5">
      <div className="flex items-center justify-between gap-2">
        <span className="flex items-center gap-2 text-[11px] text-ink-muted">
          <span
            aria-hidden
            className="size-1.5 rounded-full"
            style={{
              background: color,
              boxShadow: `0 0 0 2px color-mix(in oklab, ${color} 20%, transparent)`,
            }}
          />
          {label}
        </span>
        <span className="font-mono text-[10.5px] text-ink-subtle">{data?.version ? `v${data.version}` : ''}</span>
      </div>
    </div>
  );
}
