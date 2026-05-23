import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { fetchAdminOrder } from '@/api/orders';
import { formatDateTime, formatPrice } from '@/lib/format';

export const Route = createFileRoute('/admin/orders_/$id')({
  component: AdminOrderDetailPage,
});

function AdminOrderDetailPage() {
  const { id } = Route.useParams();
  const query = useQuery({
    queryKey: ['admin', 'orders', id],
    queryFn: () => fetchAdminOrder(id),
  });

  return (
    <div className="max-w-3xl space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-2xl font-semibold text-ink">Order detail</h1>
        <div className="flex gap-2">
          <Button asChild variant="outline" size="sm">
            <Link to="/admin/orders/$id/invoice" params={{ id }}>
              View invoice
            </Link>
          </Button>
          <Button asChild variant="outline" size="sm">
            <Link to="/admin/orders">Back to orders</Link>
          </Button>
        </div>
      </div>

      {query.isPending && <Skeleton className="h-40 w-full" />}
      {query.isError && <p className="text-sm text-status-revoked-fg">Failed to load order.</p>}

      {query.data && (
        <Card>
          <CardHeader>
            <CardTitle>Summary</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-3 text-sm">
              <Field label="Placed">{formatDateTime(query.data.createdAt)}</Field>
              <Field label="Status"><span className="capitalize">{query.data.status}</span></Field>
              <Field label="Buyer" mono>{query.data.userId}</Field>
              <Field label="Contact email">{query.data.contactEmail}</Field>
              <Field label="Items">{query.data.items.length}</Field>
              <Field label="Total">
                <div className="flex flex-col">
                  {query.data.totals.map((t) => (
                    <span key={t.currency} className="tabular-nums">{formatPrice(t.amount, t.currency)}</span>
                  ))}
                </div>
              </Field>
            </div>

            <div className="space-y-2">
              <div className="text-xs uppercase tracking-wide text-ink-subtle">Licences</div>
              {query.data.items.map((item) => (
                <div key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border p-3">
                  <div>
                    <div className="font-medium text-ink">{item.productDisplayName}</div>
                    <div className="font-mono text-[11px] text-ink-muted">{item.productSlug}</div>
                    {item.label && (
                      <div className="mt-1 text-xs text-ink-muted">
                        Label: <span className="text-ink">{item.label}</span>
                      </div>
                    )}
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-sm text-ink-muted">
                      {item.unitPrice == null ? 'Free' : formatPrice(item.unitPrice, item.currency)}
                    </span>
                    <Button asChild variant="link" size="sm" className="px-0">
                      <Link to="/admin/licences/$id" params={{ id: item.licenceId }}>
                        Open licence
                      </Link>
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function Field({ label, children, mono }: Readonly<{ label: string; children: React.ReactNode; mono?: boolean }>) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wide text-ink-subtle">{label}</div>
      <div className={mono ? 'font-mono text-xs text-ink' : 'text-sm text-ink'}>{children}</div>
    </div>
  );
}
