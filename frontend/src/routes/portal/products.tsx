import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ImageOff, LayoutGrid, List } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatDate, formatPrice } from '@/lib/format';
import { fetchProducts } from '@/api/products';
import { AddToBasketButton } from '@/components/basket/AddToBasketButton';
import type { ProductResponse } from '@/api/generated/api.schemas';

const searchSchema = z.object({
  view: z.enum(['cards', 'table']).optional(),
});

export const Route = createFileRoute('/portal/products')({
  component: PortalProductsPage,
  validateSearch: searchSchema,
});

const CARD_PAGE_SIZE = 6;
const TABLE_PAGE_SIZE = 25;

function PortalProductsPage() {
  const navigate = Route.useNavigate();
  const { view: viewParam } = Route.useSearch();
  const view = viewParam ?? 'cards';
  const pageSize = view === 'cards' ? CARD_PAGE_SIZE : TABLE_PAGE_SIZE;

  const [offset, setOffset] = useState(0);

  const query = useQuery({
    queryKey: ['portal', 'products', { offset, pageSize }],
    queryFn: () => fetchProducts({ limit: pageSize, offset }),
    placeholderData: keepPreviousData,
  });

  const setView = (next: 'cards' | 'table') => {
    if (next === view) return;
    setOffset(0);
    navigate({ search: { view: next === 'cards' ? undefined : next } }).catch(() => undefined);
  };

  const data = query.data;
  const rangeStart = data && data.total > 0 ? data.offset + 1 : 0;
  const rangeLabel = data
    ? `${rangeStart}-${Math.min(data.offset + data.limit, data.total)} of ${data.total}`
    : '';

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-semibold text-ink">Products</h1>
          <p className="text-sm text-ink-muted">Browse the available product catalog.</p>
        </div>
        <div className="inline-flex rounded-md border border-border p-0.5">
          <button
            type="button"
            onClick={() => { setView('cards'); }}
            aria-label="Card view"
            aria-pressed={view === 'cards'}
            title="Card view"
            className={cn(
              'inline-flex items-center justify-center rounded p-1.5 transition-colors',
              view === 'cards' ? 'bg-ink text-surface-elevated' : 'text-ink-muted hover:text-ink',
            )}
          >
            <LayoutGrid className="size-4" aria-hidden="true" />
          </button>
          <button
            type="button"
            onClick={() => { setView('table'); }}
            aria-label="Table view"
            aria-pressed={view === 'table'}
            title="Table view"
            className={cn(
              'inline-flex items-center justify-center rounded p-1.5 transition-colors',
              view === 'table' ? 'bg-ink text-surface-elevated' : 'text-ink-muted hover:text-ink',
            )}
          >
            <List className="size-4" aria-hidden="true" />
          </button>
        </div>
      </div>

      {query.isError && (
        <p className="text-sm text-status-revoked-fg">Failed to load products.</p>
      )}

      {view === 'cards' && (
        <CardsView isPending={query.isPending} items={data?.items ?? []} total={data?.total ?? 0} />
      )}

      {view === 'table' && (
        <TableView isPending={query.isPending} items={data?.items ?? []} total={data?.total ?? 0} />
      )}

      <div className="flex items-center justify-between text-sm text-ink-muted">
        <span>{rangeLabel}</span>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={offset === 0}
            onClick={() => setOffset(Math.max(0, offset - pageSize))}
          >
            Previous
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!data || offset + pageSize >= data.total}
            onClick={() => setOffset(offset + pageSize)}
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  );
}

interface ViewProps {
  isPending: boolean;
  items: ProductResponse[];
  total: number;
}

function CardsView({ isPending, items, total }: Readonly<ViewProps>) {
  if (isPending) {
    return (
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: CARD_PAGE_SIZE }, (_, i) => i).map((i) => (
          <div key={i} className="overflow-hidden rounded-md border border-border bg-card">
            <Skeleton className="aspect-[16/8] w-full" />
            <div className="p-3">
              <Skeleton className="h-4 w-2/3" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (total === 0) {
    return <p className="text-[12.5px] text-ink-muted">No products are available yet.</p>;
  }

  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {items.map((p) => (
        <div
          key={p.id}
          className="group overflow-hidden rounded-md border border-border bg-card shadow-card transition-shadow hover:shadow-md"
        >
          <Link
            to="/portal/products/$id"
            params={{ id: p.id }}
            aria-label={p.displayName}
            className="block"
          >
            <div
              className="relative aspect-[16/8] w-full overflow-hidden bg-surface-sunken"
              style={{
                backgroundImage:
                  'repeating-linear-gradient(135deg, color-mix(in oklab, var(--foreground) 5%, transparent) 0 1px, transparent 1px 9px)',
              }}
            >
              {p.imageUrl ? (
                <img
                  src={`/api${p.imageUrl}`}
                  alt=""
                  className="absolute inset-0 size-full object-cover"
                />
              ) : null}
              <span className="absolute left-2 top-2 rounded-[3px] bg-card/80 px-1.5 py-0.5 font-mono text-[11px] leading-none text-foreground backdrop-blur">
                {p.slug}
              </span>
              <span
                className={cn(
                  'absolute bottom-2 right-2 rounded-[3px] border px-1.5 py-0.5 font-mono text-[10.5px] leading-none backdrop-blur',
                  p.isPublic
                    ? 'border-status-active-fg/30 bg-status-active-bg text-status-active-fg'
                    : 'border-border bg-card/80 text-ink-muted',
                )}
              >
                {p.isPublic ? 'public' : 'private'}
              </span>
            </div>
            <div className="space-y-1 p-3">
              <h3 className="truncate text-[13.5px] font-semibold text-foreground">{p.displayName}</h3>
              {p.tagline && (
                <p className="line-clamp-2 text-[11.5px] text-ink-muted">{p.tagline}</p>
              )}
            </div>
          </Link>
          <div className="flex items-center justify-between border-t border-border px-3 py-2.5">
            <span className="font-medium tabular-nums">
              {p.price == null ? (
                <span className="text-[12px] text-ink-subtle">Free</span>
              ) : (
                <span className="text-[13.5px]">{formatPrice(p.price, p.currency)}</span>
              )}
            </span>
            <AddToBasketButton product={p} variant="compact" />
          </div>
        </div>
      ))}
    </div>
  );
}

function TableView({ isPending, items, total }: Readonly<ViewProps>) {
  return (
    <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-14"></TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Slug</TableHead>
            <TableHead>Price</TableHead>
            <TableHead>Available since</TableHead>
            <TableHead className="w-40 text-right">Buy</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {isPending && (
            <TableRow>
              <TableCell colSpan={6}>
                <Skeleton className="h-6 w-full" />
              </TableCell>
            </TableRow>
          )}
          {!isPending && total === 0 && (
            <TableRow>
              <TableCell colSpan={6} className="text-sm text-ink-muted">
                No products are available yet.
              </TableCell>
            </TableRow>
          )}
          {!isPending && items.map((p) => (
            <TableRow key={p.id}>
              <TableCell>
                {p.imageUrl ? (
                  <img src={`/api${p.imageUrl}`} alt="" className="size-10 rounded object-cover" />
                ) : (
                  <div className="flex size-10 items-center justify-center rounded bg-surface-sunken text-ink-subtle">
                    <ImageOff className="size-4" aria-hidden="true" />
                  </div>
                )}
              </TableCell>
              <TableCell className="font-medium text-ink">
                <Link to="/portal/products/$id" params={{ id: p.id }} className="hover:underline">
                  {p.displayName}
                </Link>
              </TableCell>
              <TableCell className="font-mono text-xs text-ink-muted">{p.slug}</TableCell>
              <TableCell className="text-ink-muted">
                {p.price == null ? 'Free' : formatPrice(p.price, p.currency)}
              </TableCell>
              <TableCell className="text-ink-muted">{formatDate(p.createdAt)}</TableCell>
              <TableCell className="text-right">
                <AddToBasketButton product={p} variant="compact" />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
