import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Plus, Search } from 'lucide-react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Button, buttonVariants } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusPill } from '@/components/StatusPill';
import { FilterChip } from '@/components/FilterChip';
import { fetchLicences } from '@/api/licences';
import { fetchProducts } from '@/api/products';
import { formatDate } from '@/lib/format';
import { cn } from '@/lib/utils';

export const Route = createFileRoute('/admin/licences')({
  component: LicencesPage,
});

const PAGE_SIZE = 25;
type StatusFilter = 'all' | 'active' | 'suspended' | 'revoked';
type ExpiresFilter = 'all' | 'expired' | 'soon';

const STATUS_OPTIONS = [
  { value: 'all', label: 'All' },
  { value: 'active', label: 'Active' },
  { value: 'suspended', label: 'Suspended' },
  { value: 'revoked', label: 'Revoked' },
] as const;

const EXPIRES_OPTIONS = [
  { value: 'all', label: 'Any time' },
  { value: 'soon', label: 'Soon (30d)' },
  { value: 'expired', label: 'Expired' },
] as const;

function LicencesPage() {
  const [status, setStatus] = useState<StatusFilter>('all');
  const [productId, setProductId] = useState<string>('all');
  const [expires, setExpires] = useState<ExpiresFilter>('all');
  const [offset, setOffset] = useState(0);

  const products = useQuery({
    queryKey: ['products-filter'],
    queryFn: () => fetchProducts({ limit: 100, offset: 0 }),
    staleTime: 60_000,
  });

  const productOptions = [
    { value: 'all', label: 'All' },
    ...(products.data?.items.map((p) => ({ value: p.id, label: p.slug })) ?? []),
  ];

  const query = useQuery({
    queryKey: ['licences', 'list', { status, productId, offset }],
    queryFn: () =>
      fetchLicences({
        status: status === 'all' ? undefined : status,
        productId: productId === 'all' ? undefined : productId,
        limit: PAGE_SIZE,
        offset,
      }),
    placeholderData: keepPreviousData,
  });

  // Independent counts for the subtitle line.
  const activeCount = useQuery({
    queryKey: ['licences-count-active'],
    queryFn: () => fetchLicences({ status: 'active', limit: 1, offset: 0 }),
    staleTime: 30_000,
  });
  const suspendedCount = useQuery({
    queryKey: ['licences-count-suspended'],
    queryFn: () => fetchLicences({ status: 'suspended', limit: 1, offset: 0 }),
    staleTime: 30_000,
  });
  const revokedCount = useQuery({
    queryKey: ['licences-count-revoked'],
    queryFn: () => fetchLicences({ status: 'revoked', limit: 1, offset: 0 }),
    staleTime: 30_000,
  });

  const data = query.data;
  const totalPages = data ? Math.max(1, Math.ceil(data.total / PAGE_SIZE)) : 1;
  const currentPage = Math.floor(offset / PAGE_SIZE) + 1;
  const rangeStart = data && data.total > 0 ? data.offset + 1 : 0;
  const rangeEnd = data ? Math.min(data.offset + data.limit, data.total) : 0;

  const filtered =
    expires === 'all'
      ? data?.items
      : data?.items.filter((lic) => {
          if (!lic.expiresAt) return false;
          const exp = new Date(lic.expiresAt).getTime();
          const now = Date.now();
          if (expires === 'expired') return exp < now;
          if (expires === 'soon') return exp > now && exp - now < 30 * 86_400_000;
          return true;
        });

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[22px] font-semibold tracking-tight text-foreground">Licences</h1>
          <p className="mt-0.5 flex items-center gap-1.5 text-[12.5px] text-ink-muted">
            <span className="tabular-nums">{activeCount.data?.total ?? 0}</span>
            {' '}active{' '}
            <span aria-hidden className="text-ink-subtle">·</span>
            <span className="tabular-nums">{suspendedCount.data?.total ?? 0}</span>
            {' '}suspended{' '}
            <span aria-hidden className="text-ink-subtle">·</span>
            <span className="tabular-nums">{revokedCount.data?.total ?? 0}</span>
            {' '}revoked
          </p>
        </div>
        <Link to="/admin/licences/new" className={cn(buttonVariants({ size: 'sm' }), 'gap-1.5')}>
          <Plus className="size-3.5" /> New licence
        </Link>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <SearchTrigger />
        <FilterChip
          label="Status"
          value={status}
          options={STATUS_OPTIONS as unknown as { value: StatusFilter; label: string }[]}
          onChange={(v) => {
            setStatus(v);
            setOffset(0);
          }}
        />
        <FilterChip
          label="Product"
          value={productId}
          options={productOptions}
          onChange={(v) => {
            setProductId(v);
            setOffset(0);
          }}
        />
        <FilterChip
          label="Expires"
          value={expires}
          options={EXPIRES_OPTIONS as unknown as { value: ExpiresFilter; label: string }[]}
          onChange={(v) => setExpires(v)}
        />
      </div>

      <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
        <Table className="text-[12.5px]">
          <TableHeader>
            <TableRow className="border-border">
              <Th>Licence</Th>
              <Th>Product</Th>
              <Th>Customer</Th>
              <Th className="w-[100px]">Status</Th>
              <Th className="w-[80px]">HWID</Th>
              <Th className="w-[110px]">Expires</Th>
              <Th className="w-[110px]">Created</Th>
              <Th className="w-[60px]" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {query.isPending && (
              <TableRow>
                <TableCell colSpan={8}>
                  <Skeleton className="h-6 w-full" />
                </TableCell>
              </TableRow>
            )}
            {query.isError && (
              <TableRow>
                <TableCell colSpan={8} className="text-status-revoked-fg">
                  Failed to load licences.
                </TableCell>
              </TableRow>
            )}
            {filtered?.map((lic) => (
              <TableRow key={lic.id} className="border-border hover:bg-surface-sunken">
                <Td>
                  <Link
                    to="/admin/licences/$id"
                    params={{ id: lic.id }}
                    className="font-mono text-[11.5px] text-foreground hover:underline"
                  >
                    {lic.id.length > 16 ? `${lic.id.slice(0, 16)}` : lic.id}
                  </Link>
                </Td>
                <Td>
                  <span className="font-mono text-[11.5px] text-foreground">{lic.productSlug}</span>
                </Td>
                <Td>
                  <span className="text-ink-muted">{lic.userEmail}</span>
                </Td>
                <Td>
                  <StatusPill status={lic.status} />
                </Td>
                <Td>
                  {lic.hwidBound ? (
                    <span className="inline-flex items-center gap-1 text-[12px] text-status-active-fg">
                      <span aria-hidden className="size-1.5 rounded-full bg-status-active-fg" /> Bound
                    </span>
                  ) : (
                    <span className="text-ink-subtle">-</span>
                  )}
                </Td>
                <Td className="font-mono text-[11.5px] text-ink-muted">
                  {lic.expiresAt ? formatDate(lic.expiresAt) : '-'}
                </Td>
                <Td className="font-mono text-[11.5px] text-ink-muted">{formatDate(lic.createdAt)}</Td>
                <Td>
                  <Link
                    to="/admin/licences/$id"
                    params={{ id: lic.id }}
                    className="text-[12px] text-accent hover:underline"
                  >
                    View
                  </Link>
                </Td>
              </TableRow>
            ))}
            {filtered?.length === 0 && !query.isError && (
              <TableRow>
                <TableCell colSpan={8} className="text-ink-muted">
                  No licences match this filter.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <div className="flex items-center justify-between text-[12px] text-ink-muted">
        <span className="font-mono tabular-nums">
          {rangeStart}-{rangeEnd} of {data?.total ?? 0}
        </span>
        <div className="flex items-center gap-1.5">
          <Button
            variant="outline"
            size="icon"
            className="size-7"
            disabled={offset === 0}
            onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}
            aria-label="Previous page"
          >
            <ChevronLeft className="size-3.5" />
          </Button>
          <span className="font-mono text-[11.5px] tabular-nums">
            {currentPage} / {totalPages}
          </span>
          <Button
            variant="outline"
            size="icon"
            className="size-7"
            disabled={!data || offset + PAGE_SIZE >= data.total}
            onClick={() => setOffset(offset + PAGE_SIZE)}
            aria-label="Next page"
          >
            <ChevronRight className="size-3.5" />
          </Button>
        </div>
      </div>
    </div>
  );
}

function Th({ children, className }: Readonly<{ children?: React.ReactNode; className?: string }>) {
  return (
    <TableHead
      className={cn(
        'h-9 px-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted',
        className,
      )}
    >
      {children}
    </TableHead>
  );
}

function Td({ children, className }: Readonly<{ children?: React.ReactNode; className?: string }>) {
  return <TableCell className={cn('px-3 py-2.5', className)}>{children}</TableCell>;
}

function SearchTrigger() {
  const onClick = () => {
    globalThis.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'k', metaKey: true, ctrlKey: true, bubbles: true }),
    );
  };
  return (
    <button
      type="button"
      onClick={onClick}
      className="group inline-flex h-7 w-[280px] items-center gap-2 rounded-[4px] border border-border bg-card px-2.5 text-left text-[12px] text-ink-muted transition-colors hover:bg-surface-sunken"
    >
      <Search className="size-3.5 text-ink-subtle" aria-hidden />
      <span className="flex-1 truncate">Search by key, email, HWID...</span>
      <span className="rounded border border-border px-1 py-0 font-mono text-[10.5px] text-ink-subtle">⌘K</span>
    </button>
  );
}
