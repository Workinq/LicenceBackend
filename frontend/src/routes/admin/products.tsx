import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Button, buttonVariants } from '@/components/ui/button';
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
import { fetchProducts } from '@/api/products';
import { fetchLicences } from '@/api/licences';

const searchSchema = z.object({
  view: z.enum(['cards', 'table']).optional(),
});

export const Route = createFileRoute('/admin/products')({
  component: ProductsPage,
  validateSearch: searchSchema,
});

const CARD_PAGE_SIZE = 6;
const TABLE_PAGE_SIZE = 25;

function formatPrice(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

function ProductsPage() {
  const navigate = Route.useNavigate();
  const { view: viewParam } = Route.useSearch();
  const view = viewParam ?? 'table';
  const pageSize = view === 'cards' ? CARD_PAGE_SIZE : TABLE_PAGE_SIZE;

  const [search, setSearch] = useState('');
  const [offset, setOffset] = useState(0);

  const trimmed = search.trim();
  const query = useQuery({
    queryKey: ['products', 'list', { q: trimmed, offset, pageSize }],
    queryFn: () => fetchProducts({ q: trimmed || undefined, limit: pageSize, offset }),
    placeholderData: keepPreviousData,
  });

  const onSearchChange = (next: string) => {
    setSearch(next);
    setOffset(0);
  };

  const setView = (next: 'cards' | 'table') => {
    if (next === view) return;
    setOffset(0);
    navigate({ search: { view: next === 'table' ? undefined : next } }).catch(() => undefined);
  };

  const data = query.data;
  const rangeStart = data && data.total > 0 ? data.offset + 1 : 0;
  const rangeLabel = data
    ? `${rangeStart}-${Math.min(data.offset + data.limit, data.total)} of ${data.total}`
    : '';

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-2xl font-semibold text-ink">Products</h1>
        <Link to="/admin/products/new" className={buttonVariants()}>
          New product
        </Link>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <Input
          type="search"
          placeholder="Search products by name"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          className="max-w-sm"
        />
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
        <CardsView
          isPending={query.isPending}
          items={data?.items ?? []}
          total={data?.total ?? 0}
          searching={trimmed.length > 0}
        />
      )}

      {view === 'table' && (
        <TableView
          isPending={query.isPending}
          items={data?.items ?? []}
          total={data?.total ?? 0}
          searching={trimmed.length > 0}
        />
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
  items: ProductRow[];
  total: number;
  searching: boolean;
}

type ProductRow = {
  id: string;
  slug: string;
  displayName: string;
  isPublic: boolean;
  price: number | null;
  currency: string;
  imageUrl: string | null;
  tagline: string | null;
  createdAt: string;
};

function CardsView({ isPending, items, total, searching }: Readonly<ViewProps>) {
  if (isPending) {
    return (
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: CARD_PAGE_SIZE }, (_, i) => i).map((i) => (
          <Card key={i} className="overflow-hidden py-0 gap-0">
            <Skeleton className="aspect-video w-full" />
            <CardHeader className="p-3">
              <Skeleton className="h-4 w-2/3" />
            </CardHeader>
          </Card>
        ))}
      </div>
    );
  }

  if (total === 0) {
    return (
      <p className="text-sm text-ink-muted">
        {searching ? 'No products match your search.' : 'No products yet. Create one to get started.'}
      </p>
    );
  }

  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {items.map((p) => (
        <Link
          key={p.id}
          to="/admin/products/$id"
          params={{ id: p.id }}
          className="block rounded-lg transition-shadow hover:shadow-card"
        >
          <Card className="overflow-hidden py-0 gap-0">
            {p.imageUrl ? (
              <img
                src={`/api${p.imageUrl}`}
                alt=""
                className="aspect-video w-full object-cover"
              />
            ) : (
              <div className="flex aspect-video w-full items-center justify-center bg-surface-sunken text-ink-subtle">
                <ImageOff className="size-5" aria-hidden="true" />
              </div>
            )}
            <CardHeader className="p-3 gap-1">
              <div className="flex items-start justify-between gap-2">
                <CardTitle className="truncate text-sm">{p.displayName}</CardTitle>
                <Badge variant={p.isPublic ? 'default' : 'secondary'} className="text-[10px]">
                  {p.isPublic ? 'Public' : 'Private'}
                </Badge>
              </div>
              <CardDescription className="font-mono text-[11px]">{p.slug}</CardDescription>
            </CardHeader>
            <CardContent className="px-3 pb-3 text-xs">
              {p.price == null ? (
                <span className="text-ink-subtle">No price</span>
              ) : (
                formatPrice(p.price, p.currency)
              )}
            </CardContent>
          </Card>
        </Link>
      ))}
    </div>
  );
}

function TableView({ isPending, items, total, searching }: Readonly<ViewProps>) {
  return (
    <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
      <Table className="text-[12.5px]">
        <TableHeader>
          <TableRow className="border-border">
            <TableHead className="h-9 px-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Slug</TableHead>
            <TableHead className="h-9 px-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Display name</TableHead>
            <TableHead className="h-9 w-[110px] px-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Visibility</TableHead>
            <TableHead className="h-9 w-[100px] px-3 text-right text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Price</TableHead>
            <TableHead className="h-9 w-[90px] px-3 text-right text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Licences</TableHead>
            <TableHead className="h-9 w-[90px] px-3 text-right text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">MRR</TableHead>
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
              <TableCell colSpan={6} className="text-ink-muted">
                {searching ? 'No products match your search.' : 'No products yet. Create one to get started.'}
              </TableCell>
            </TableRow>
          )}
          {!isPending && items.map((p) => <ProductTableRow key={p.id} product={p} />)}
        </TableBody>
      </Table>
    </div>
  );
}

function ProductTableRow({ product }: Readonly<{ product: ProductRow }>) {
  const licenceCount = useQuery({
    queryKey: ['product-licence-count', product.id],
    queryFn: () => fetchLicences({ productId: product.id, limit: 1, offset: 0 }),
    staleTime: 60_000,
  });

  return (
    <TableRow className="border-border hover:bg-surface-sunken">
      <TableCell className="px-3 py-2.5">
        <Link
          to="/admin/products/$id"
          params={{ id: product.id }}
          className="font-mono text-[11.5px] text-foreground hover:underline"
        >
          {product.slug}
        </Link>
      </TableCell>
      <TableCell className="px-3 py-2.5">{product.displayName}</TableCell>
      <TableCell className="px-3 py-2.5">
        <Badge
          variant={product.isPublic ? 'default' : 'secondary'}
          className={cn(
            'h-5 rounded-[3px] px-1.5 text-[10.5px] font-medium',
            product.isPublic
              ? 'border border-status-active-fg/30 bg-status-active-bg text-status-active-fg'
              : 'bg-surface-sunken text-ink-muted',
          )}
        >
          {product.isPublic ? 'public' : 'private'}
        </Badge>
      </TableCell>
      <TableCell className="px-3 py-2.5 text-right font-mono tabular-nums text-ink-muted">
        {product.price == null ? '-' : formatPrice(product.price, product.currency)}
      </TableCell>
      <TableCell className="px-3 py-2.5 text-right font-mono tabular-nums text-foreground">
        {licenceCount.data?.total ?? '-'}
      </TableCell>
      <TableCell className="px-3 py-2.5 text-right font-mono font-medium tabular-nums text-foreground">
        -
      </TableCell>
    </TableRow>
  );
}
