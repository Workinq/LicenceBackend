import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { fetchProducts } from '@/api/products';
import { fetchLicences } from '@/api/licences';
import { Skeleton } from '@/components/ui/skeleton';

export function TopProducts({ limit = 5 }: Readonly<{ limit?: number }>) {
  const products = useQuery({
    queryKey: ['products-for-top'],
    queryFn: () => fetchProducts({ limit: 30, offset: 0 }),
    staleTime: 30_000,
  });

  const items = products.data?.items ?? [];

  return (
    <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
      <div className="border-b border-border px-4 py-2.5">
        <h2 className="text-[13px] font-semibold text-foreground">Top products</h2>
        <p className="text-[11.5px] text-ink-muted">By active licences</p>
      </div>
      <ol className="divide-y divide-border">
        {products.isPending && (
          <li className="p-4">
            <Skeleton className="h-5 w-full" />
          </li>
        )}
        {products.data &&
          items.slice(0, limit).map((p) => (
            <ProductRow key={p.id} productId={p.id} slug={p.slug} maxLicences={products.data?.total ?? 0} />
          ))}
        {products.data && items.length === 0 && (
          <li className="p-4 text-[12.5px] text-ink-muted">No products yet.</li>
        )}
      </ol>
    </div>
  );
}

function ProductRow({
  productId,
  slug,
  maxLicences,
}: Readonly<{
  productId: string;
  slug: string;
  maxLicences: number;
}>) {
  const licences = useQuery({
    queryKey: ['licences-count', productId],
    queryFn: () => fetchLicences({ productId, limit: 1, offset: 0 }),
    staleTime: 30_000,
  });

  const count = licences.data?.total ?? 0;
  const share = maxLicences > 0 ? Math.min(100, (count / Math.max(1, maxLicences)) * 100) : 0;

  return (
    <li className="px-4 py-2.5">
      <div className="flex items-center justify-between gap-3">
        <Link
          to="/admin/products/$id"
          params={{ id: productId }}
          className="font-mono text-[12px] text-foreground hover:underline"
        >
          {slug}
        </Link>
        <span className="font-mono text-[11.5px] tabular-nums text-ink-muted">{count}</span>
      </div>
      <div className="mt-1.5 h-1 w-full overflow-hidden rounded-full bg-surface-sunken">
        <div className="h-full rounded-full bg-accent" style={{ width: `${share}%` }} />
      </div>
    </li>
  );
}
