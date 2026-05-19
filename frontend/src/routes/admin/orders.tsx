import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
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
import { fetchAdminOrders } from '@/api/orders';
import { formatDateTime, formatPrice } from '@/lib/format';

export const Route = createFileRoute('/admin/orders')({
  component: AdminOrdersPage,
});

const PAGE_SIZE = 25;

function AdminOrdersPage() {
  const [offset, setOffset] = useState(0);
  const [userId, setUserId] = useState('');

  const query = useQuery({
    queryKey: ['admin', 'orders', { offset, userId }],
    queryFn: () => fetchAdminOrders({ limit: PAGE_SIZE, offset, userId: userId.trim() || undefined }),
    placeholderData: keepPreviousData,
  });

  const data = query.data;
  const rangeLabel = data
    ? `${data.total === 0 ? 0 : data.offset + 1}-${Math.min(data.offset + data.limit, data.total)} of ${data.total}`
    : '';

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-semibold text-ink">Orders</h1>
          <p className="text-sm text-ink-muted">All orders across the platform.</p>
        </div>
        <div className="flex items-center gap-2">
          <input
            type="text"
            value={userId}
            onChange={(e) => { setUserId(e.target.value); setOffset(0); }}
            placeholder="Filter by user id"
            className="h-9 w-72 rounded-md border border-border bg-surface-elevated px-3 text-sm focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
        </div>
      </div>

      {query.isError && <p className="text-sm text-status-revoked-fg">Failed to load orders.</p>}

      <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Placed</TableHead>
              <TableHead>Buyer</TableHead>
              <TableHead>Items</TableHead>
              <TableHead>Total</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-24"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {query.isPending && (
              <TableRow>
                <TableCell colSpan={6}>
                  <Skeleton className="h-6 w-full" />
                </TableCell>
              </TableRow>
            )}
            {!query.isPending && (data?.total ?? 0) === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-sm text-ink-muted">
                  No orders match.
                </TableCell>
              </TableRow>
            )}
            {!query.isPending && data?.items.map((order) => (
              <TableRow key={order.id}>
                <TableCell className="text-ink-muted">{formatDateTime(order.createdAt)}</TableCell>
                <TableCell className="font-mono text-xs text-ink-muted">{order.userId}</TableCell>
                <TableCell>{order.items.length}</TableCell>
                <TableCell>
                  <div className="flex flex-col">
                    {order.totals.map((t) => (
                      <span key={t.currency} className="tabular-nums">{formatPrice(t.amount, t.currency)}</span>
                    ))}
                  </div>
                </TableCell>
                <TableCell className="capitalize text-ink-muted">{order.status}</TableCell>
                <TableCell>
                  <Button asChild variant="link" size="sm" className="px-0">
                    <Link to="/admin/orders/$id" params={{ id: order.id }}>
                      View
                    </Link>
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="flex items-center justify-between text-sm text-ink-muted">
        <span>{rangeLabel}</span>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" disabled={offset === 0} onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}>
            Previous
          </Button>
          <Button variant="outline" size="sm" disabled={!data || offset + PAGE_SIZE >= data.total} onClick={() => setOffset(offset + PAGE_SIZE)}>
            Next
          </Button>
        </div>
      </div>
    </div>
  );
}
