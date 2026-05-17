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

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

function ProductsPage() {
  const navigate = Route.useNavigate();
  const { view: viewParam } = Route.useSearch();
  const view = viewParam ?? 'cards';
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
    void navigate({ search: { view: next === 'cards' ? undefined : next } });
  };

  const data = query.data;
  const rangeLabel = data
    ? `${data.total === 0 ? 0 : data.offset + 1}-${Math.min(data.offset + data.limit, data.total)} of ${data.total}`
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

function CardsView({ isPending, items, total, searching }: ViewProps) {
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
              {p.price != null ? (
                formatPrice(p.price, p.currency)
              ) : (
                <span className="text-ink-subtle">No price</span>
              )}
            </CardContent>
          </Card>
        </Link>
      ))}
    </div>
  );
}

function TableView({ isPending, items, total, searching }: ViewProps) {
  return (
    <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-14"></TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Slug</TableHead>
            <TableHead>Visibility</TableHead>
            <TableHead>Price</TableHead>
            <TableHead>Created</TableHead>
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
                {searching ? 'No products match your search.' : 'No products yet. Create one to get started.'}
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
              <TableCell>
                <Link
                  to="/admin/products/$id"
                  params={{ id: p.id }}
                  className="font-medium text-ink underline-offset-2 hover:underline"
                >
                  {p.displayName}
                </Link>
              </TableCell>
              <TableCell className="font-mono text-xs text-ink-muted">{p.slug}</TableCell>
              <TableCell>
                <Badge variant={p.isPublic ? 'default' : 'secondary'}>
                  {p.isPublic ? 'Public' : 'Private'}
                </Badge>
              </TableCell>
              <TableCell className="text-ink-muted">
                {p.price != null ? formatPrice(p.price, p.currency) : '-'}
              </TableCell>
              <TableCell className="text-ink-muted">{formatDate(p.createdAt)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
