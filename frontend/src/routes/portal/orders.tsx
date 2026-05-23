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
import { fetchMyOrders } from '@/api/orders';
import { formatDateTime, formatPrice } from '@/lib/format';

export const Route = createFileRoute('/portal/orders')({
  component: OrdersListPage,
});

const PAGE_SIZE = 25;

function OrdersListPage() {
  const [offset, setOffset] = useState(0);
  const query = useQuery({
    queryKey: ['portal', 'orders', { offset, limit: PAGE_SIZE }],
    queryFn: () => fetchMyOrders({ limit: PAGE_SIZE, offset }),
    placeholderData: keepPreviousData,
  });

  const data = query.data;
  const rangeStart = data && data.total > 0 ? data.offset + 1 : 0;
  const rangeLabel = data
    ? `${rangeStart}-${Math.min(data.offset + data.limit, data.total)} of ${data.total}`
    : '';

  return (
    <div className="space-y-4">
      <h1 className="font-display text-2xl font-semibold text-ink">My orders</h1>

      <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Placed</TableHead>
              <TableHead>Items</TableHead>
              <TableHead>Total</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-24"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {query.isPending && (
              <TableRow>
                <TableCell colSpan={5}>
                  <Skeleton className="h-6 w-full" />
                </TableCell>
              </TableRow>
            )}
            {query.isError && (
              <TableRow>
                <TableCell colSpan={5} className="text-sm text-status-revoked-fg">
                  Failed to load orders.
                </TableCell>
              </TableRow>
            )}
            {!query.isPending && !query.isError && (data?.total ?? 0) === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-sm text-ink-muted">
                  You haven't placed any orders yet.
                </TableCell>
              </TableRow>
            )}
            {!query.isPending && data?.items.map((order) => {
              const itemCount = order.items.length;
              return (
                <TableRow key={order.id}>
                  <TableCell className="text-ink-muted">{formatDateTime(order.createdAt)}</TableCell>
                  <TableCell>{itemCount}</TableCell>
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
                      <Link to="/portal/orders/$id" params={{ id: order.id }}>
                        View
                      </Link>
                    </Button>
                  </TableCell>
                </TableRow>
              );
            })}
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
